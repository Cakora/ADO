/*
Bulk upsert by unique Name (SQL Server) with XML or JSON input.

Input XML format:
<Items>
  <Item SourceId="1" Name="Alice" />
  <Item SourceId="2" Name="Bob" />
</Items>

Input JSON format:
{
  "Items": [
    { "SourceId": 1, "Name": "Alice" },
    { "SourceId": 2, "Name": "Bob" }
  ]
}

Behavior:
- For each Item, match destination row by Name (case sensitivity depends on DB collation).
- If Name exists: reuse existing Id.
- If Name is new: insert and generate Id.
- Marks destination row as imported (IsImported = 1).
- Returns a result set mapping SourceId -> DestinationId with Success/Inserted flags.
- Processes distinct names in batches (default 500) to keep locks/logging predictable on large payloads.
- Recommended for parallel callers: keep the unique index on Name; the script also uses key-range locks for the NOT EXISTS check.
*/

-- Demo table (adjust schema/table/columns as needed)
IF OBJECT_ID(N'dbo.ItemCatalog', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ItemCatalog
    (
        Id          BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ItemCatalog PRIMARY KEY,
        [Name]      NVARCHAR(200) NOT NULL,
        IsImported  BIT NOT NULL CONSTRAINT DF_ItemCatalog_IsImported DEFAULT (0),
        CreatedAt   DATETIME2(3) NOT NULL CONSTRAINT DF_ItemCatalog_CreatedAt DEFAULT (SYSUTCDATETIME())
    );
    CREATE UNIQUE INDEX UX_ItemCatalog_Name ON dbo.ItemCatalog([Name]);
END
GO

CREATE OR ALTER PROCEDURE dbo.UpsertItemCatalogFromXml
    @ItemsXml XML,
    @BatchSize INT = 500
AS
BEGIN
    SET NOCOUNT ON;

    IF @BatchSize IS NULL OR @BatchSize <= 0
        SET @BatchSize = 500;

    DECLARE @Input TABLE
    (
        SourceId BIGINT NOT NULL,
        [Name] NVARCHAR(200) NULL
    );

    INSERT INTO @Input(SourceId, [Name])
    SELECT
        T.X.value('@SourceId[1]', 'bigint') AS SourceId,
        NULLIF(LTRIM(RTRIM(T.X.value('@Name[1]', 'nvarchar(200)'))), N'') AS [Name]
    FROM @ItemsXml.nodes('/Items/Item') AS T(X);

    -- Work with distinct names; batch the work to keep writes predictable.
    DECLARE @DistinctNames TABLE
    (
        RowNum INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [Name] NVARCHAR(200) NOT NULL UNIQUE
    );

    INSERT INTO @DistinctNames([Name])
    SELECT DISTINCT [Name]
    FROM @Input
    WHERE [Name] IS NOT NULL;

    -- Snapshot existing names so we can compute "Inserted" in the final output.
    DECLARE @Existing TABLE([Name] NVARCHAR(200) NOT NULL PRIMARY KEY);
    INSERT INTO @Existing([Name])
    SELECT DISTINCT t.[Name]
    FROM dbo.ItemCatalog t
    INNER JOIN @DistinctNames i
        ON i.[Name] = t.[Name];

    DECLARE @StartRow INT = 1;
    DECLARE @TotalRows INT = (SELECT COUNT(1) FROM @DistinctNames);

    WHILE @StartRow <= @TotalRows
    BEGIN
        DECLARE @EndRow INT = @StartRow + (@BatchSize - 1);

        -- Insert missing names (no MERGE).
        INSERT INTO dbo.ItemCatalog([Name], IsImported)
        SELECT b.[Name], 1
        FROM
        (
            SELECT [Name]
            FROM @DistinctNames
            WHERE RowNum BETWEEN @StartRow AND @EndRow
        ) b
        WHERE NOT EXISTS (SELECT 1 FROM dbo.ItemCatalog t WITH (UPDLOCK, HOLDLOCK) WHERE t.[Name] = b.[Name]);

        -- Update existing matches to mark imported (inserted rows already have IsImported = 1).
        UPDATE t
            SET t.IsImported = 1
        FROM dbo.ItemCatalog t
        INNER JOIN
        (
            SELECT [Name]
            FROM @DistinctNames
            WHERE RowNum BETWEEN @StartRow AND @EndRow
        ) b ON b.[Name] = t.[Name]
        INNER JOIN @Existing e ON e.[Name] = t.[Name];

        SET @StartRow = @StartRow + @BatchSize;
    END

    -- Return mapping list: one row per input item.
    SELECT
        i.SourceId,
        t.Id AS DestinationId,
        CAST(CASE WHEN i.[Name] IS NOT NULL AND e.[Name] IS NULL THEN 1 ELSE 0 END AS bit) AS Inserted,
        CAST(CASE WHEN i.[Name] IS NOT NULL THEN 1 ELSE 0 END AS bit) AS Success,
        CAST(CASE WHEN i.[Name] IS NULL THEN N'Name is required' ELSE NULL END AS nvarchar(200)) AS ErrorMessage
    FROM @Input i
    LEFT JOIN dbo.ItemCatalog t ON t.[Name] = i.[Name]
    LEFT JOIN @Existing e ON e.[Name] = i.[Name]
    ORDER BY i.SourceId;
END
GO

CREATE OR ALTER PROCEDURE dbo.UpsertItemCatalogFromJson
    @ItemsJson NVARCHAR(MAX),
    @BatchSize INT = 500
AS
BEGIN
    SET NOCOUNT ON;

    IF @BatchSize IS NULL OR @BatchSize <= 0
        SET @BatchSize = 500;

    DECLARE @Input TABLE
    (
        SourceId BIGINT NOT NULL,
        [Name] NVARCHAR(200) NULL
    );

    INSERT INTO @Input(SourceId, [Name])
    SELECT
        j.SourceId,
        NULLIF(LTRIM(RTRIM(j.[Name])), N'') AS [Name]
    FROM OPENJSON(@ItemsJson, '$.Items')
    WITH
    (
        SourceId BIGINT         '$.SourceId',
        [Name]   NVARCHAR(200)  '$.Name'
    ) j;

    DECLARE @DistinctNames TABLE
    (
        RowNum INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [Name] NVARCHAR(200) NOT NULL UNIQUE
    );

    INSERT INTO @DistinctNames([Name])
    SELECT DISTINCT [Name]
    FROM @Input
    WHERE [Name] IS NOT NULL;

    DECLARE @Existing TABLE([Name] NVARCHAR(200) NOT NULL PRIMARY KEY);
    INSERT INTO @Existing([Name])
    SELECT DISTINCT t.[Name]
    FROM dbo.ItemCatalog t
    INNER JOIN @DistinctNames i
        ON i.[Name] = t.[Name];

    DECLARE @StartRow INT = 1;
    DECLARE @TotalRows INT = (SELECT COUNT(1) FROM @DistinctNames);

    WHILE @StartRow <= @TotalRows
    BEGIN
        DECLARE @EndRow INT = @StartRow + (@BatchSize - 1);

        INSERT INTO dbo.ItemCatalog([Name], IsImported)
        SELECT b.[Name], 1
        FROM
        (
            SELECT [Name]
            FROM @DistinctNames
            WHERE RowNum BETWEEN @StartRow AND @EndRow
        ) b
        WHERE NOT EXISTS (SELECT 1 FROM dbo.ItemCatalog t WITH (UPDLOCK, HOLDLOCK) WHERE t.[Name] = b.[Name]);

        UPDATE t
            SET t.IsImported = 1
        FROM dbo.ItemCatalog t
        INNER JOIN
        (
            SELECT [Name]
            FROM @DistinctNames
            WHERE RowNum BETWEEN @StartRow AND @EndRow
        ) b ON b.[Name] = t.[Name]
        INNER JOIN @Existing e ON e.[Name] = t.[Name];

        SET @StartRow = @StartRow + @BatchSize;
    END

    SELECT
        i.SourceId,
        t.Id AS DestinationId,
        CAST(CASE WHEN i.[Name] IS NOT NULL AND e.[Name] IS NULL THEN 1 ELSE 0 END AS bit) AS Inserted,
        CAST(CASE WHEN i.[Name] IS NOT NULL THEN 1 ELSE 0 END AS bit) AS Success,
        CAST(CASE WHEN i.[Name] IS NULL THEN N'Name is required' ELSE NULL END AS nvarchar(200)) AS ErrorMessage
    FROM @Input i
    LEFT JOIN dbo.ItemCatalog t ON t.[Name] = i.[Name]
    LEFT JOIN @Existing e ON e.[Name] = i.[Name]
    ORDER BY i.SourceId;
END
GO
