# What To Use (Recommended Public Surface)

This page is a short “use only these” guide for application teams.

---

## 1) Create and Use an Executor

### A) Direct (no DI)

Use:

- `AdoAsync.DbOptions`
- `AdoAsync.Execution.DbExecutor.Create(...)`
- `AdoAsync.Abstractions.IDbExecutor` methods

```csharp
using System.Data;
using AdoAsync;
using AdoAsync.Execution;

var options = new DbOptions
{
    DatabaseType = DatabaseType.SqlServer,
    ConnectionString = "...",
    CommandTimeoutSeconds = 30
};

await using var executor = DbExecutor.Create(options);
(int RowsAffected, IReadOnlyDictionary<string, object?> OutputParameters) ping =
    await executor.ExecuteAsync(new CommandDefinition { CommandText = "select 1", CommandType = CommandType.Text });
var rows = ping.RowsAffected;
```

### B) DI (recommended for apps)

Use:

- `AdoAsync.DependencyInjection.AdoAsyncServiceCollectionExtensions`
- `AdoAsync.Abstractions.IDbExecutorFactory`

```csharp
using AdoAsync.DependencyInjection;

builder.Services.AddAdoAsync("Main", new DbOptions
{
    DatabaseType = DatabaseType.SqlServer,
    ConnectionString = "...",
    CommandTimeoutSeconds = 30
});

builder.Services.AddAdoAsyncExecutor("Main"); // Scoped IDbExecutor
```

---

## 2) Reads (Choose One)

### A) Fast streaming (SQL Server + PostgreSQL only)

Use:

- `IDbExecutor.StreamAsync(...)` → `IAsyncEnumerable<IDataRecord>`
- `AdoAsync.Execution.DbExecutorQueryExtensions.QueryAsync<T>(...)` (mapping convenience)
- `AdoAsync.Common.DataRecordExtensions.Get<T>(...)` for safe typed reads

```csharp
using AdoAsync.Common;
using AdoAsync.Execution;

await foreach (var customer in executor.QueryAsync(
    new CommandDefinition { CommandText = "select Id, Name from dbo.Customers" },
    record => new Customer(record.Get<int>(0) ?? 0, record.Get<string>(1) ?? "")))
{
}
```

Do not use streaming when:

- Provider is `Oracle`

More combinations (streaming + buffered + materializers):
- `docs/conversion-extensions.md`
- You need output parameters
- PostgreSQL/Oracle refcursor procedures (use buffered)

### B) Buffered DataTable (all providers)

Use:

- `IDbExecutor.QueryTableAsync(...)` → `(DataTable Table, IReadOnlyDictionary<string, object?> OutputParameters)`

```csharp
(DataTable Table, IReadOnlyDictionary<string, object?> OutputParameters) result =
    await executor.QueryTableAsync(new CommandDefinition { CommandText = "select Id, Name from dbo.Customers" });

var table = result.Table;
var outputs = result.OutputParameters;
```

### C) Buffered List<T> (all providers)

Use:

- `IDbExecutor.QueryAsync<T>(..., Func<DataRow,T> map)` → `(List<T> Rows, IReadOnlyDictionary<string, object?> OutputParameters)`
- More examples: `docs/query-examples-with-output-parameters.md`

```csharp
(List<Customer> Rows, IReadOnlyDictionary<string, object?> OutputParameters) result = await executor.QueryAsync(
    new CommandDefinition { CommandText = "select Id, Name from dbo.Customers" },
    row => new Customer(row.Field<int>("Id"), row.Field<string>("Name")!));

var customers = result.Rows;
```

### D) Buffered multi-result DataSet (all providers)

Use:

- `IDbExecutor.ExecuteDataSetAsync(...)` → `(DataSet DataSet, IReadOnlyDictionary<string, object?> OutputParameters)`

```csharp
(DataSet DataSet, IReadOnlyDictionary<string, object?> OutputParameters) result =
    await executor.ExecuteDataSetAsync(new CommandDefinition
{
    CommandText = "dbo.GetCustomerAndOrders",
    CommandType = CommandType.StoredProcedure
});

var dataSet = result.DataSet;
var outputs = result.OutputParameters;
var customers = dataSet.Tables[0];
var orders = dataSet.Tables[1];
```

---

## 3) Writes (Choose One)

### A) Simple write

Use:

- `IDbExecutor.ExecuteAsync(...)` → `(int RowsAffected, IReadOnlyDictionary<string, object?> OutputParameters)`

```csharp
(int RowsAffected, IReadOnlyDictionary<string, object?> OutputParameters) result =
    await executor.ExecuteAsync(new CommandDefinition
{
    CommandText = "update dbo.Items set Processed = 1 where Id = @id",
    CommandType = CommandType.Text,
    Parameters = new[]
    {
        new DbParameter { Name = "@id", DataType = DbDataType.Int32, Direction = ParameterDirection.Input, Value = 42 }
    }
});

var affected = result.RowsAffected;
```

### B) Bulk insert (provider-native)

Use:

- `DbExecutor.BulkImportAsync(BulkImportRequest)` → `BulkImportResult`

### C) Bulk insert (typed via linq2db)

Use:

- `DbExecutor.BulkImportAsync<T>(IEnumerable<T>/IAsyncEnumerable<T>, ...)` → `BulkImportResult`
- Docs: `docs/linq2db.md`

### D) Bulk update/upsert pattern

Use:

- Bulk load staging (B or C)
- Apply staging → target with one set-based SQL command
- Docs: `docs/bulk-update.md`

---

## 4) Optional Mapping Extensions (Use Only If You Want)

These are safe to use after you already have a `DataTable` / `DataSet`:

- `AdoAsync.Extensions.Execution.DataTableExtensions.ToList(...)` (DataTable → List)
- Docs: `docs/conversion-extensions.md`
- `AdoAsync.Extensions.Execution.DataSetMapExtensions.MapTablesToArrays(...)` (DataSet → arrays)

---

## 5) Do Not Use These Directly (Library Internals)

Application code should avoid referencing these namespaces/classes directly:

- `AdoAsync.Providers.*` (provider implementations)
- `AdoAsync.Helpers.*` (adapter/parameter/provider helpers)
- `AdoAsync.Validation.*` (validators)
- `AdoAsync.Resilience.*` (retry policy internals)

Stick to the API in sections 1–4 above.
