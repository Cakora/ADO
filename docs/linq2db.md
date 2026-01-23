# linq2db Typed Bulk Copy (AdoAsync)

This document covers the **typed** linq2db bulk copy APIs exposed by `DbExecutor`.

If you want the provider-native bulk path (SqlBulkCopy / PostgreSQL COPY / OracleBulkCopy), see `IDbExecutor` docs in `docs/idbexecutor.md`.

---

## 1) What this feature is

Typed bulk copy lets you bulk insert rows from:

- `IEnumerable<T>`
- `IAsyncEnumerable<T>`

…without requiring a `DbDataReader` (unlike `BulkImportRequest`).

The executor calls linq2db internally and returns a `BulkImportResult`:

- `Success` (`true/false`)
- `RowsInserted`
- `Duration`
- `Error` (when failed)

---

## 2) Enablement (Required)

Typed bulk copy is **disabled by default**. Enable it via:

```csharp
using AdoAsync;
using AdoAsync.BulkCopy.LinqToDb.Common;
using AdoAsync.Execution;

var options = new DbOptions
{
    DatabaseType = DatabaseType.SqlServer,
    ConnectionString = "...",
    CommandTimeoutSeconds = 30,
    LinqToDb = new LinqToDbBulkOptions
    {
        Enable = true
    }
};

await using var executor = DbExecutor.Create(options);
```

If `Enable = false`, `BulkImportAsync<T>` returns `Success = false` with a configuration error.

---

## 3) API Surface (Inputs and Outputs)

### 3.1 IEnumerable path

Method:

```csharp
ValueTask<BulkImportResult> BulkImportAsync<T>(
    IEnumerable<T> items,
    string? tableName = null,
    LinqToDbBulkOptions? bulkOptions = null,
    CancellationToken cancellationToken = default)
    where T : class;
```

Inputs:

- `items` (required): the rows to insert
- `tableName` (optional): override destination table (if omitted, linq2db mapping rules decide)
- `bulkOptions` (optional): per-call overrides (merged with `DbOptions.LinqToDb`)

Output:

- `BulkImportResult`

### 3.2 IAsyncEnumerable path

Method:

```csharp
ValueTask<BulkImportResult> BulkImportAsync<T>(
    IAsyncEnumerable<T> items,
    string? tableName = null,
    LinqToDbBulkOptions? bulkOptions = null,
    CancellationToken cancellationToken = default)
    where T : class;
```

---

## 4) Bulk Options (`LinqToDbBulkOptions`)

The following options can be set in `DbOptions.LinqToDb` (defaults) or passed per call (overrides).

Commonly used:

- `Enable` (bool): must be `true` to use typed bulk copy
- `BulkCopyType` (linq2db `BulkCopyType`): controls bulk mode
- `BulkCopyTimeoutSeconds` (int?)
- `MaxBatchSize` (int?)
- `NotifyAfter` (int?)
- `KeepIdentity` (bool?)
- `CheckConstraints` (bool?)
- `KeepNulls` (bool?)
- `FireTriggers` (bool?)
- `TableLock` (bool?)
- `UseInternalTransaction` (bool?)
- `UseParameters` (bool?)
- `MaxParametersForBatch` (int?)
- `MaxDegreeOfParallelism` (int?)
- `OnRowsCopied` (callback)

Merge behavior:

- `Enable` becomes `overrides.Enable || defaults.Enable`
- Most other fields use override when provided, else default

---

## 5) Complete Example (Inputs + Output)

### 5.1 Input rows (POCO)

```csharp
public sealed class ItemRow
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
}
```

### 5.2 Bulk insert (IEnumerable)

```csharp
using AdoAsync;
using AdoAsync.BulkCopy.LinqToDb.Common;
using AdoAsync.Execution;

var options = new DbOptions
{
    DatabaseType = DatabaseType.SqlServer,
    ConnectionString = "...",
    CommandTimeoutSeconds = 30,
    LinqToDb = new LinqToDbBulkOptions { Enable = true }
};

await using var executor = DbExecutor.Create(options);

var items = new[]
{
    new ItemRow { Id = 1, Name = "A" },
    new ItemRow { Id = 2, Name = "B" }
};

var result = await executor.BulkImportAsync(
    items,
    tableName: "dbo.Items",
    bulkOptions: new LinqToDbBulkOptions
    {
        MaxBatchSize = 10_000,
        NotifyAfter = 10_000
    });

if (!result.Success)
{
    throw new Exception(result.Error!.MessageKey);
}

Console.WriteLine($"RowsInserted={result.RowsInserted}, Duration={result.Duration}");
```

Expected output shape (example):

- `Success` → `true`
- `RowsInserted` → `2`
- `Duration` → e.g. `00:00:00.0123456`

### 5.3 Bulk insert (IAsyncEnumerable)

```csharp
static async IAsyncEnumerable<ItemRow> GetRows()
{
    yield return new ItemRow { Id = 1, Name = "A" };
    yield return new ItemRow { Id = 2, Name = "B" };
}

var result = await executor.BulkImportAsync(
    GetRows(),
    tableName: "dbo.Items",
    bulkOptions: new LinqToDbBulkOptions { MaxBatchSize = 10_000 });
```

---

## 6) Transactions and Retries

- `BulkImportAsync<T>` enlists in the current executor transaction started by `BeginTransactionAsync()`.
- If a user transaction is active, Polly retries are skipped (same rule as other executor operations).

Example:

```csharp
await using var tx = await executor.BeginTransactionAsync();
var r1 = await executor.BulkImportAsync(items1, tableName: "dbo.Items");
var r2 = await executor.BulkImportAsync(items2, tableName: "dbo.Items2");
await tx.CommitAsync();
```

---

## 7) Provider Notes

The typed linq2db path is designed to work across all supported providers:

- SQL Server
- PostgreSQL
- Oracle

Your `DatabaseType` in `DbOptions` must match the target database, and the correct linq2db provider packages must be referenced (already included in `src/AdoAsync/AdoAsync.csproj`).

