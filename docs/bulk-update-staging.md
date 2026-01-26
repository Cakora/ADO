# Bulk Update — “Send Once” Patterns (SQL Server 2012 + Oracle 12c)

This file shows **3 minimal patterns**:
1) **XML payload (works for both providers)**
2) **SQL Server TVP**
3) **Oracle GTT + array-binding (best Oracle option)**

Each pattern is shown in this order:
1) **Input format**
2) **C# command call**
3) **Procedure (minimal)**

All examples work inside an AdoAsync transaction:
- start: `await using var tx = await executor.BeginTransactionAsync();`
- commit: `await tx.CommitAsync();`

---

## 1) XML payload (Both SQL Server + Oracle)

### 1.1 Input format (XML)

```xml
<rows>
  <row destinationId="101" state="READY" taskName="ImportCustomers" updatedOn="2026-01-25T00:00:00Z" />
  <row destinationId="102" state="FAILED" taskName="ImportOrders" />
</rows>
```

### 1.2 C# command call (send once)

```csharp
// Inputs: batchId (string), xml (string)
//
// SQL Server: dbo.ApplyDestinationStatus_Xml(@BatchId, @Xml)
// Oracle:     APPLY_DESTINATION_STATUS_XML(:p_batch_id, :p_xml)
//
// AdoAsync example:
// await using var tx = await executor.BeginTransactionAsync();
// await executor.ExecuteAsync(new CommandDefinition
// {
//     CommandText = options.DatabaseType == DatabaseType.SqlServer
//         ? "dbo.ApplyDestinationStatus_Xml"
//         : "APPLY_DESTINATION_STATUS_XML",
//     CommandType = CommandType.StoredProcedure,
//     Parameters = options.DatabaseType == DatabaseType.SqlServer
//         ? new DbParameter[]
//         {
//             new() { Name = "@BatchId", DataType = DbDataType.String, Direction = ParameterDirection.Input, Value = batchId },
//             new() { Name = "@Xml", DataType = DbDataType.Xml, Direction = ParameterDirection.Input, Value = xmlPayload }
//         }
//         : new DbParameter[]
//         {
//             new() { Name = ":p_batch_id", DataType = DbDataType.String, Direction = ParameterDirection.Input, Value = batchId },
//             new() { Name = ":p_xml", DataType = DbDataType.Clob, Direction = ParameterDirection.Input, Value = xmlPayload }
//         }
// });
// await tx.CommitAsync();
```

### 1.3 SQL Server 2012 procedure (minimal)

```sql
CREATE PROCEDURE dbo.ApplyDestinationStatus_Xml
    @BatchId NVARCHAR(36),
    @Xml     XML
AS
BEGIN
    SET NOCOUNT ON;

    ;WITH x AS
    (
        SELECT
            T.X.value('@destinationId','int') AS DestinationId,
            T.X.value('@state','nvarchar(50)') AS NewState,
            T.X.value('@taskName','nvarchar(200)') AS TaskName
        FROM @Xml.nodes('/rows/row') AS T(X)
    )
    UPDATE t
    SET
        t.State = x.NewState,
        t.TaskName = x.TaskName
    FROM dbo.DestinationStatus t
    INNER JOIN x ON x.DestinationId = t.DestinationId;
END
GO
```

### 1.4 Oracle 12c procedure (minimal)

```sql
CREATE OR REPLACE PROCEDURE APPLY_DESTINATION_STATUS_XML(
    p_batch_id IN VARCHAR2,
    p_xml      IN CLOB
) AS
BEGIN
    MERGE INTO DESTINATION_STATUS t
    USING (
        SELECT
            TO_NUMBER(EXTRACTVALUE(VALUE(x), '/row/@destinationId')) AS DESTINATION_ID,
            EXTRACTVALUE(VALUE(x), '/row/@state') AS NEW_STATE,
            EXTRACTVALUE(VALUE(x), '/row/@taskName') AS TASK_NAME
        FROM TABLE(XMLSEQUENCE(XMLTYPE(p_xml).EXTRACT('/rows/row'))) x
    ) s
    ON (t.DESTINATION_ID = s.DESTINATION_ID)
    WHEN MATCHED THEN
        UPDATE SET
            t.STATE = s.NEW_STATE,
            t.TASK_NAME = s.TASK_NAME;
END;
/
```

---

## 2) SQL Server TVP (best SQL Server option)

### 2.1 Input format (TVP row shape)

TVP row columns:
- `BatchId` (nvarchar(36))
- `DestinationId` (int)
- `State` (nvarchar)
- `TaskName` (nvarchar)
- `UpdatedOn` (datetime2, nullable)

### 2.2 C# command call (send once)

```csharp
// Build DataTable columns: BatchId, DestinationId, State, TaskName, UpdatedOn
// Call dbo.LoadStage_DestinationStatus(@Rows TVP) using DbDataType.Structured + StructuredTypeName
// Then call dbo.ApplyStage_DestinationStatus(@BatchId)

// Example (AdoAsync):
// using AdoAsync.Providers.SqlServer;
// var tvp = new DataTable();
// tvp.Columns.Add("BatchId", typeof(string));
// tvp.Columns.Add("DestinationId", typeof(int));
// tvp.Columns.Add("State", typeof(string));
// tvp.Columns.Add("TaskName", typeof(string));
// tvp.Columns.Add("UpdatedOn", typeof(DateTime));
// ...
// await executor.ExecuteAsync(new CommandDefinition
// {
//     CommandText = "dbo.LoadStage_DestinationStatus",
//     CommandType = CommandType.StoredProcedure,
//     Parameters = new[]
//     {
//         tvp.ToTvp(parameterName: "@Rows", structuredTypeName: "dbo.Stage_DestinationStatus_Row")
//     }
// });
```

### 2.3 SQL Server 2012 objects (minimal)

```sql
CREATE TABLE dbo.Stage_DestinationStatus
(
    BatchId NVARCHAR(36) NOT NULL,
    DestinationId INT NOT NULL,
    State NVARCHAR(50) NOT NULL,
    TaskName NVARCHAR(200) NULL,
    UpdatedOn DATETIME2(0) NULL
);
GO

CREATE TYPE dbo.Stage_DestinationStatus_Row AS TABLE
(
    BatchId NVARCHAR(36) NOT NULL,
    DestinationId INT NOT NULL,
    State NVARCHAR(50) NOT NULL,
    TaskName NVARCHAR(200) NULL,
    UpdatedOn DATETIME2(0) NULL
);
GO

CREATE PROCEDURE dbo.LoadStage_DestinationStatus
    @Rows dbo.Stage_DestinationStatus_Row READONLY
AS
BEGIN
    INSERT INTO dbo.Stage_DestinationStatus (BatchId, DestinationId, State, TaskName, UpdatedOn)
    SELECT BatchId, DestinationId, State, TaskName, UpdatedOn
    FROM @Rows;
END
GO

CREATE PROCEDURE dbo.ApplyStage_DestinationStatus
    @BatchId NVARCHAR(36)
AS
BEGIN
    UPDATE t
    SET
        t.State = s.State,
        t.TaskName = s.TaskName,
        t.UpdatedOn = ISNULL(s.UpdatedOn, SYSUTCDATETIME())
    FROM dbo.DestinationStatus t
    INNER JOIN dbo.Stage_DestinationStatus s ON s.DestinationId = t.DestinationId
    WHERE s.BatchId = @BatchId;

    DELETE FROM dbo.Stage_DestinationStatus WHERE BatchId = @BatchId;
END
GO
```

---

## 3) Oracle 12c GTT + array-binding (recommended for list input)

This is the closest Oracle equivalent to “table variable” semantics while still allowing a clean `MERGE` join:
- Rows are **session-scoped** (safe with many concurrent users).
- Use `ON COMMIT DELETE ROWS` so data auto-clears on commit.

### 3.1 Input format (list → arrays)

Your C# list represents **Key + values** (example: 1 key + 3 values):
- Key: `DestinationId`
- Values: `State`, `TaskName`, `UpdatedOn`

The procedure receives the list as arrays. All arrays must have **same length** and represent the same row by index:
- `p_destination_id[i]`
- `p_state[i]`
- `p_task_name[i]`
- `p_updated_on[i]` (can be null)

### 3.2 C# command call (send once)

```csharp
// Create arrays in a single loop (avoid index mismatch).
// Call DESTINATION_STATUS_PKG.APPLY_DESTINATION_STATUS_LIST(...) once.

// Example (AdoAsync):
// using AdoAsync.Providers.Oracle;
// await using var tx = await executor.BeginTransactionAsync();
// var parameters = new[]
// {
//     rows.ToArrayBindingParameter(":p_destination_id", DbDataType.Int32, r => r.DestinationId),
//     // NOTE: For Oracle string arrays, Size is required (max length per element).
//     rows.ToArrayBindingParameter(":p_state", DbDataType.String, r => r.State, size: 50),
//     rows.ToArrayBindingParameter(":p_task_name", DbDataType.String, r => r.TaskName, size: 200),
//     rows.ToArrayBindingParameter(":p_updated_on", DbDataType.DateTime, r => r.UpdatedOnUtc ?? default)
// };
//
// await executor.ExecuteAsync(new CommandDefinition
// {
//     CommandText = "DESTINATION_STATUS_PKG.APPLY_DESTINATION_STATUS_LIST",
//     CommandType = CommandType.StoredProcedure,
//     Parameters = parameters
// });
// await tx.CommitAsync();
```

### 3.3 Oracle 12c objects (minimal)

```sql
-- Global Temporary Table (definition is permanent; rows are per-session)
CREATE GLOBAL TEMPORARY TABLE GTT_DESTINATION_STATUS_UPD
(
    DESTINATION_ID  NUMBER(10)     NOT NULL,
    STATE           VARCHAR2(50)   NOT NULL,
    TASK_NAME       VARCHAR2(200)  NULL,
    UPDATED_ON  DATE           NULL
)
ON COMMIT DELETE ROWS;
/

CREATE INDEX IX_GTT_DESTSTAT_UPD_ID ON GTT_DESTINATION_STATUS_UPD (DESTINATION_ID);
/

CREATE OR REPLACE PACKAGE DESTINATION_STATUS_PKG AS
    TYPE t_number IS TABLE OF NUMBER INDEX BY PLS_INTEGER;
    TYPE t_varchar2_50 IS TABLE OF VARCHAR2(50) INDEX BY PLS_INTEGER;
    TYPE t_varchar2_200 IS TABLE OF VARCHAR2(200) INDEX BY PLS_INTEGER;
    TYPE t_date IS TABLE OF DATE INDEX BY PLS_INTEGER;

    PROCEDURE APPLY_DESTINATION_STATUS_LIST(
        p_destination_id IN t_number,
        p_state          IN t_varchar2_50,
        p_task_name      IN t_varchar2_200,
        p_updated_on IN t_date
    );
END DESTINATION_STATUS_PKG;
/

CREATE OR REPLACE PACKAGE BODY DESTINATION_STATUS_PKG AS
    PROCEDURE APPLY_DESTINATION_STATUS_LIST(
        p_destination_id IN t_number,
        p_state          IN t_varchar2_50,
        p_task_name      IN t_varchar2_200,
        p_updated_on IN t_date
    ) AS
    BEGIN
        -- 1) Load list into the session-scoped GTT
        FORALL i IN INDICES OF p_destination_id
            INSERT INTO GTT_DESTINATION_STATUS_UPD (DESTINATION_ID, STATE, TASK_NAME, UPDATED_ON)
            VALUES (p_destination_id(i), p_state(i), p_task_name(i), p_updated_on(i));

        -- 2) Join + update (set-based)
        MERGE INTO DESTINATION_STATUS t
        USING GTT_DESTINATION_STATUS_UPD s
        ON (t.DESTINATION_ID = s.DESTINATION_ID)
        WHEN MATCHED THEN
            UPDATE SET
                t.STATE = s.STATE,
                t.TASK_NAME = s.TASK_NAME,
                t.UPDATED_ON = NVL(s.UPDATED_ON, SYSDATE);
    END APPLY_DESTINATION_STATUS_LIST;
END DESTINATION_STATUS_PKG;
/
```
