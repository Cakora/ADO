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
var rows = await executor.ExecuteAsync(new CommandDefinition { CommandText = "select 1", CommandType = CommandType.Text });
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
- You need output parameters
- PostgreSQL/Oracle refcursor procedures (use buffered)

### B) Buffered DataTable (all providers)

Use:

- `IDbExecutor.QueryTableAsync(...)` → `DataTable`
- `AdoAsync.Extensions.Execution.OutputParameterExtensions.GetOutputParameters(table)`

```csharp
using AdoAsync.Extensions.Execution;

var table = await executor.QueryTableAsync(new CommandDefinition { CommandText = "select Id, Name from dbo.Customers" });
var outputs = table.GetOutputParameters(); // null if none
```

### C) Buffered List<T> (all providers)

Use:

- `IDbExecutor.QueryAsync<T>(..., Func<DataRow,T> map)` → `List<T>`

```csharp
var customers = await executor.QueryAsync(
    new CommandDefinition { CommandText = "select Id, Name from dbo.Customers" },
    row => new Customer(row.Field<int>("Id"), row.Field<string>("Name")!));
```

### D) Buffered multi-result DataSet (all providers)

Use:

- `IDbExecutor.ExecuteDataSetAsync(...)` → `DataSet`
- `OutputParameterExtensions.GetOutputParameters(dataSet)`

```csharp
using AdoAsync.Extensions.Execution;

var dataSet = await executor.ExecuteDataSetAsync(new CommandDefinition
{
    CommandText = "dbo.GetCustomerAndOrders",
    CommandType = CommandType.StoredProcedure
});

var outputs = dataSet.GetOutputParameters();
var customers = dataSet.Tables[0];
var orders = dataSet.Tables[1];
```

---

## 3) Writes (Choose One)

### A) Simple write

Use:

- `IDbExecutor.ExecuteAsync(...)` → affected rows

```csharp
var affected = await executor.ExecuteAsync(new CommandDefinition
{
    CommandText = "update dbo.Items set Processed = 1 where Id = @id",
    CommandType = CommandType.Text,
    Parameters = new[]
    {
        new DbParameter { Name = "@id", DataType = DbDataType.Int32, Direction = ParameterDirection.Input, Value = 42 }
    }
});
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
- `AdoAsync.Extensions.Execution.MultiResultMapExtensions.MapTablesToArrays(...)` (DataSet/MultiResult → arrays)
- `AdoAsync.Extensions.Execution.DataSetExtensions.ToMultiResult(...)` (DataSet → MultiResult)

---

## 5) Do Not Use These Directly (Library Internals)

Application code should avoid referencing these namespaces/classes directly:

- `AdoAsync.Providers.*` (provider implementations)
- `AdoAsync.Helpers.*` (adapter/parameter/provider helpers)
- `AdoAsync.Validation.*` (validators)
- `AdoAsync.Resilience.*` (retry policy internals)

Stick to the API in sections 1–4 above.

