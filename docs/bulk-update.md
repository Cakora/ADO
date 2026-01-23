# Bulk Update / Bulk Upsert (Separate Pattern)

This repo’s built-in bulk feature is **insert-only**:

- `DbExecutor.BulkImportAsync(BulkImportRequest)` (provider-native)
- `DbExecutor.BulkImportAsync<T>` (linq2db typed bulk copy)

If you need **bulk update** or **upsert**, the recommended approach is a 2-step pattern:

1) Bulk import into a staging table (fast insert)
2) Run a set-based `UPDATE` / `MERGE` / `INSERT ... ON CONFLICT` from staging into the target table

This document explains that pattern and provides provider-specific SQL templates.

---

## 1) High-Level Flow (Inputs and Outputs)

### Inputs

- Target table (the “real” table you want to update/upsert)
- Staging table (temporary/permanent)
- Bulk payload rows (your source data)
- Match key(s) (e.g., `Name`, `ExternalId`, `(TenantId, Name)`, etc.)
- Columns to update

### Outputs

- Rows inserted / updated (provider-dependent)
- Optional error details if your “apply” step fails

---

## 2) Recommended Execution Order (Fastest Way)

1. Start a transaction (`BeginTransactionAsync`) when you want all-or-nothing behavior.
2. Bulk import into staging (fastest insert path available to you).
3. Execute the provider-specific upsert SQL to apply staging → target.
4. Truncate/cleanup staging (optional).
5. Commit the transaction.

---

## 3) Step 1: Bulk Import into Staging (AdoAsync)

Use provider-native bulk:

```csharp
using var reader = GetSourceReader(); // DbDataReader for staging columns

var request = new BulkImportRequest
{
    DestinationTable = "dbo.Stage_Items",
    SourceReader = reader,
    ColumnMappings = new[]
    {
        new BulkImportColumnMapping { SourceColumn = "Name", DestinationColumn = "Name" },
        new BulkImportColumnMapping { SourceColumn = "Value", DestinationColumn = "Value" }
    },
    AllowedDestinationTables = new HashSet<string> { "dbo.Stage_Items" },
    AllowedDestinationColumns = new HashSet<string> { "Name", "Value" }
};

var bulk = await executor.BulkImportAsync(request);
if (!bulk.Success) throw new Exception(bulk.Error!.MessageKey);
```

Or use typed linq2db (see `docs/linq2db.md`) if that fits your input pipeline better.

---

## 4) Step 2: Apply Staging → Target (Provider SQL)

These templates are **documentation-only** and live in:

- `docs/sql-scripts/bulk-upsert-by-name-sqlserver.sql`
- `docs/sql-scripts/bulk-upsert-by-name-postgresql.sql`
- `docs/sql-scripts/bulk-upsert-by-name-oracle.sql`

You can run them using `executor.ExecuteAsync(new CommandDefinition { ... })` once you’ve bulk-loaded staging.

### 4.1 SQL Server (concept)

Typical approaches:

- `MERGE` (be careful with known pitfalls)
- `UPDATE` + `INSERT` with `NOT EXISTS`

Template file:

- `docs/sql-scripts/bulk-upsert-by-name-sqlserver.sql`

### 4.2 PostgreSQL (concept)

Typical approaches:

- `INSERT ... ON CONFLICT (...) DO UPDATE`
- `UPDATE ... FROM stage`

Template file:

- `docs/sql-scripts/bulk-upsert-by-name-postgresql.sql`

### 4.3 Oracle (concept)

Typical approaches:

- `MERGE`
- `UPDATE` + `INSERT` with `NOT EXISTS`

Template file:

- `docs/sql-scripts/bulk-upsert-by-name-oracle.sql`

---

## 5) Complete Example (Inputs + Output)

### Goal

Upsert `Items` by `Name`:

- If `Name` exists → update `Value`
- Else → insert new row

### Example command sequence

```csharp
await using var tx = await executor.BeginTransactionAsync();

// 1) bulk load stage
var bulk = await executor.BulkImportAsync(stageRequest);
if (!bulk.Success) throw new Exception(bulk.Error!.MessageKey);

// 2) apply stage -> target (provider-specific SQL)
(int RowsAffected, IReadOnlyDictionary<string, object?> OutputParameters) applyResult =
    await executor.ExecuteAsync(new CommandDefinition
{
    CommandText = "/* paste provider-specific upsert SQL here */",
    CommandType = CommandType.Text
});

await tx.CommitAsync();

Console.WriteLine($"StageRowsInserted={bulk.RowsInserted}, ApplyRowsAffected={applyResult.RowsAffected}");
```

Expected output shape (example):

- `bulk.Success` → `true`
- `bulk.RowsInserted` → staging row count
- `affected` → rows updated + inserted (provider-specific)

---

## 6) Practical Notes

- For large loads, keep the apply step set-based (no row-by-row loops).
- Put a unique index on the match key (e.g., `Name`) in the target table.
- Clean up staging after apply (truncate or partition-based cleanup).
- If you need per-row error reporting, capture invalid rows into a reject table before applying.
