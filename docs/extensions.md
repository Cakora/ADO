# Extensions (How Many, Where, How To Use)

This repo contains **14** extension classes (`static class ...Extensions`) across:

- `src/AdoAsync/Extensions/...`
- `src/AdoAsync.Common/...`

Only a small subset is meant to be used directly by application code.

---

## Caller-Facing Extensions

## Why Some Extensions Were Internal

Several extensions in `AdoAsync.Extensions.Execution` were originally marked `internal` intentionally:

- They are implementation helpers (performance + allocation control).
- Keeping them internal prevents the public API from growing with methods we may want to change later.

If you are using AdoAsync as a library consumer, you typically **use the public `IDbExecutor` / `DbExecutor` methods**.
Some mapping helpers are also exposed as extensions so you can map `DataTable` / `DataSet` results without writing `for` loops.

### 1) Dependency Injection

File: `src/AdoAsync/Extensions/DependencyInjection/AdoAsyncServiceCollectionExtensions.cs`

Namespace: `AdoAsync.DependencyInjection`

Use cases:

- Register an `IDbExecutorFactory`
- Register named databases (multi-DB)
- Register a scoped `IDbExecutor`

Examples:

```csharp
using AdoAsync.DependencyInjection;

builder.Services.AddAdoAsyncFactory();
```

```csharp
using AdoAsync.DependencyInjection;

builder.Services.AddAdoAsync("Main", new DbOptions
{
    DatabaseType = DatabaseType.SqlServer,
    ConnectionString = "...",
    CommandTimeoutSeconds = 30
});

builder.Services.AddAdoAsyncExecutor("Main");
```

### 2) Output parameters

Output parameters are returned from `IDbExecutor` methods via tuples (no DataTable/DataSet output extensions).

### 3) Result-set typed getters (recommended for streaming)

File: `src/AdoAsync.Common/DataRecordExtensions.cs`

Namespace: `AdoAsync.Common`

Methods:

- `Get<T>(this IDataRecord record, int ordinal)`
- `Get<T>(this IDataRecord record, string name)`

Example:

```csharp
using AdoAsync.Common;

var id = record.Get<int>("id");
var name = record.Get<string>("name");
```

---

## Internal Extensions (Library-Only) — What They Do + How You Use The Feature

Below are the internal helpers you listed, with:

- what they do inside the library, and
- the public way you use that capability from your application code.

---

## IAsyncEnumerable (Streaming) — Where To Use / Where Not To Use

### Use `IAsyncEnumerable<T>` when:

- You want **row-by-row processing** with low memory usage.
- You want **fast reads** and you don’t need output parameters.
- You are on **SQL Server or PostgreSQL** (Oracle streaming is not supported here).

APIs that return streaming sequences:

- `IDbExecutor.StreamAsync(...)` → `IAsyncEnumerable<IDataRecord>`
- `AdoAsync.Execution.DbExecutorQueryExtensions.QueryAsync<T>(...)` → `IAsyncEnumerable<T>` (mapping convenience; added in 1.5)

### Do NOT use `IAsyncEnumerable<T>` when:

- You need to return a `List<T>` / `DataTable` / `DataSet` to another layer (buffer instead).
- You need **output parameters** (use buffered methods, or `ExecuteReaderAsync` + `GetOutputParametersAsync()` where supported).
- You need **multi-result** sets (`DataSet` / `QueryTablesAsync`).
- Provider is **Oracle** (streaming APIs throw unsupported by design).
- PostgreSQL procedure returns **refcursors** (must use buffered `QueryTablesAsync` / `QueryTableAsync`).

### Streaming examples (detailed)

#### A) Best streaming mapping (recommended)

```csharp
using System.Data;
using AdoAsync;
using AdoAsync.Common;
using AdoAsync.Execution; // QueryAsync<T> extension

await using var executor = DbExecutor.Create(options);

var command = new CommandDefinition
{
    CommandText = "select Id, Name from dbo.Customers where Id >= @minId",
    CommandType = CommandType.Text,
    Parameters = new[]
    {
        new DbParameter { Name = "@minId", DataType = DbDataType.Int32, Direction = ParameterDirection.Input, Value = 100 }
    }
};

await foreach (var customer in executor.QueryAsync(
    command,
    record => new Customer(
        Id: record.Get<int>(0) ?? 0,
        Name: record.Get<string>(1) ?? "")))
{
    // process each row (no buffering)
}
```

#### A2) Complete copy/paste example (minimal)

```csharp
using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using AdoAsync;
using AdoAsync.Common;
using AdoAsync.Execution;

public sealed record Customer(int Id, string Name);

public static class Demo
{
    public static async Task RunAsync(CancellationToken cancellationToken = default)
    {
        var options = new DbOptions
        {
            DatabaseType = DatabaseType.SqlServer, // or PostgreSql
            ConnectionString = "...",
            CommandTimeoutSeconds = 30
        };

        await using var executor = DbExecutor.Create(options);

        var command = new CommandDefinition
        {
            CommandText = "select Id, Name from dbo.Customers",
            CommandType = CommandType.Text
        };

        await foreach (var customer in executor.QueryAsync(
            command,
            record => new Customer(
                record.Get<int>(0) ?? 0,
                record.Get<string>(1) ?? string.Empty),
            cancellationToken))
        {
            Console.WriteLine($"{customer.Id} - {customer.Name}");
        }
    }
}
```

#### B) Stream with cancellation

```csharp
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

await foreach (var customer in executor.QueryAsync(command, MapCustomer, cts.Token))
{
    // cancel stops enumeration
}

static Customer MapCustomer(IDataRecord r) =>
    new(r.GetInt32(0), r.GetString(1));
```

#### C) What NOT to do with streaming

Do not return the `IAsyncEnumerable<T>` from a method that disposes the executor before enumeration:

```csharp
// BAD: executor disposed before caller enumerates.
public IAsyncEnumerable<Customer> GetCustomersBad()
{
    using var executor = DbExecutor.Create(options); // disposed too early
    return executor.QueryAsync(new CommandDefinition { CommandText = "select ..." }, r => new Customer(...));
}
```

Instead, either:

- enumerate inside the method, or
- keep the executor alive for the enumeration lifetime (DI Scoped works well for this).

---

## Fastest Buffered Mapping (DataTable / DataSet → List)

When you choose buffered APIs, these are the fastest patterns (ordered):

### 1) Fastest DataTable → `List<T>` (extension, no LINQ)

Use `QueryTableAsync` + `DataTableExtensions.ToList(...)`:

```csharp
using System.Data;
using AdoAsync.Extensions.Execution;

(DataTable Table, IReadOnlyDictionary<string, object?> OutputParameters) tableResult =
    await executor.QueryTableAsync(new CommandDefinition
{
    CommandText = "select Id, Name from dbo.Customers",
    CommandType = CommandType.Text
});

DataTable table = tableResult.Table;

var customers = table.ToList(row => new Customer(
    Id: row.Field<int>("Id"),
    Name: row.Field<string>("Name")!));
```

### 2) Fastest “one call” DataTable → `List<T>`

Use `DbExecutor.QueryAsync<T>(DataRow map)` which internally uses a fast indexed loop (no LINQ allocations):

```csharp
(List<Customer> Rows, IReadOnlyDictionary<string, object?> OutputParameters) result = await executor.QueryAsync(
    new CommandDefinition
    {
        CommandText = "select Id, Name from dbo.Customers",
        CommandType = CommandType.Text
    },
    row => new Customer(
        Id: row.Field<int>("Id"),
        Name: row.Field<string>("Name")!));

var customers = result.Rows;
```

When to use:

- You want a `List<T>` and you’re okay buffering the full result.

When NOT to use:

- Very large result sets where streaming is better.

### 3) Fastest DataSet → `List<T>` per table (multi-result)

Use `ExecuteDataSetAsync` + `DataTableExtensions.ToList(...)` per table:

```csharp
using System.Data;
using AdoAsync.Extensions.Execution;

(DataSet DataSet, IReadOnlyDictionary<string, object?> OutputParameters) dataSetResult =
    await executor.ExecuteDataSetAsync(new CommandDefinition
{
    CommandText = "dbo.GetCustomerAndOrders",
    CommandType = CommandType.StoredProcedure
});

DataSet dataSet = dataSetResult.DataSet;

var customersTable = dataSet.Tables[0];
var ordersTable = dataSet.Tables[1];

var customers = customersTable.ToList(row =>
    new Customer(row.Field<int>("Id"), row.Field<string>("Name")!));

var orders = ordersTable.ToList(row =>
    new Order(row.Field<int>("Id"), row.Field<int>("CustomerId")));
```

### 3.1 DataSet → “List of tables” → “List<T> per table”

If you want to map all tables with the same row mapper, use `MultiResultMapExtensions.MapTables(...)` instead of manual loops:

```csharp
using AdoAsync.Extensions.Execution;

IReadOnlyList<List<Customer>> mapped = dataSet.MapTables(row =>
    new Customer(row.Field<int>("Id"), row.Field<string>("Name")!));
```

### 4) Maximum read performance (buffered) → arrays

If the next layer benefits from arrays (fastest iteration), you can convert lists to arrays or map with `MapTablesToArrays` when tables share the same output type.

```csharp
using AdoAsync.Extensions.Execution;

(DataTable Table, IReadOnlyDictionary<string, object?> OutputParameters) tableResult =
    await executor.QueryTableAsync(new CommandDefinition { CommandText = "select Id, Name from dbo.Customers" });
var table = tableResult.Table;

var customers = table
    .ToList(row => new Customer(row.Field<int>("Id"), row.Field<string>("Name")!))
    .ToArray();
```

Example: DataSet where every table maps to the *same* output type:

```csharp
using AdoAsync.Extensions.Execution;

var dataSetResult = await executor.ExecuteDataSetAsync(new CommandDefinition { CommandText = "..." });
var dataSet = dataSetResult.DataSet;
var arrays = dataSet.MapTablesToArrays(row => row.Field<int>(0));
```

---

### `DbDataReaderExtensions` (streaming list/record helpers)

Internal methods:

- `StreamRecordsAsync(DbDataReader)` → `IAsyncEnumerable<IDataRecord>`
- `ToListAsync<T>(DbDataReader, map)` → `List<T>`

Public way to use (stream rows):

```csharp
await foreach (var record in executor.StreamAsync(new CommandDefinition { CommandText = "select ..." }))
{
    // record is IDataRecord
}
```

Public way to use (stream + map with the new extension added in 1.5):

```csharp
using AdoAsync.Common;
using AdoAsync.Execution;

await foreach (Customer customer in executor.QueryAsync(
    new CommandDefinition { CommandText = "select Id, Name from dbo.Customers" },
    record => new Customer(
        record.Get<int>(0) ?? 0,
        record.Get<string>(1) ?? "")))
{
}
```

### `DataTableExtensions` (fast DataRow mapping)

Internal method:

- `ToList<T>(DataTable, map)` → `List<T>` (fast indexed loop)

Public way to use (get `DataTable`):

```csharp
var tableResult = await executor.QueryTableAsync(new CommandDefinition { CommandText = "select ..." });
var table = tableResult.Table;
```

Public way to use (get `List<T>` directly, uses the internal fast mapping):

```csharp
var customersResult = await executor.QueryAsync(
    new CommandDefinition { CommandText = "select Id, Name from dbo.Customers" },
    row => new Customer(row.Field<int>("Id"), row.Field<string>("Name")!));
var customers = customersResult.Rows;
```

Where NOT to use this:

- If result is very large and you don’t want memory growth → use streaming `QueryAsync<T>` (IAsyncEnumerable) instead.

### `DataSetExtensions` (convert DataSet → MultiResult)

Internal method:

- `ToMultiResult(DataSet, outputParameters)` → `MultiResult`

Public way to use (get `DataSet` + output parameters):

```csharp
using AdoAsync.Extensions.Execution;

(DataSet DataSet, IReadOnlyDictionary<string, object?> OutputParameters) dataSetResult =
    await executor.ExecuteDataSetAsync(new CommandDefinition
{
    CommandText = "dbo.GetCustomerAndOrders",
    CommandType = CommandType.StoredProcedure
});

var dataSet = dataSetResult.DataSet;
var outputs = dataSetResult.OutputParameters;
```

For `QueryTablesAsync` (DbExecutor-only), outputs are not returned because it only returns tables; use `ExecuteDataSetAsync` / `QueryTableAsync` if you need outputs.

When to use DataSet:

- Multi-result stored procedures (SQL Server multi-result / PostgreSQL refcursor / Oracle refcursor)
- When you need `DataTable` objects (interop, legacy code)

When NOT to use DataSet:

- For large single-result reads where streaming is enough (use `StreamAsync` / `QueryAsync<T>`)

Public way to convert to `MultiResult` (if you use `MultiResult` in your app code):

```csharp
using AdoAsync.Extensions.Execution;

(DataSet DataSet, IReadOnlyDictionary<string, object?> OutputParameters) dataSetResult =
    await executor.ExecuteDataSetAsync(new CommandDefinition
    {
        CommandText = "dbo.GetCustomerAndOrders",
        CommandType = CommandType.StoredProcedure
    });

MultiResult multi = dataSetResult.DataSet.ToMultiResult(dataSetResult.OutputParameters);
```

### `MultiResultMapExtensions` (fast mapping patterns for buffered multi-results)

Internal methods (examples):

- `dataSet.MapTables(...)` → map each table to list/collection
- `multiResult.MapTablesToArrays(...)` → fastest mapping to arrays

Public way to use (no manual loops):

```csharp
using AdoAsync.Extensions.Execution;

// Map every table to a List<T> using a single mapper
IReadOnlyList<List<Customer>> perTableLists = dataSet.MapTables(row =>
    new Customer(row.Field<int>("Id"), row.Field<string>("Name")!));

// Fastest buffered mapping: map every table to an array
IReadOnlyList<Customer[]> perTableArrays = dataSet.MapTablesToArrays(row =>
    new Customer(row.Field<int>("Id"), row.Field<string>("Name")!));
```

### `SpanMappingExtensions` (low-allocation array projection)

Internal method:

- `sourceArray.MapToArray(map)` (span-based loop)

Public way to use:

```csharp
using AdoAsync.Extensions.Execution;

int[] source = new[] { 1, 2, 3 };
int[] dest = source.MapToArray(x => x * 10);
```

### `ValueNormalizationExtensions` (normalize provider-returned scalars)

Internal methods:

- `NormalizeByType(value, DbDataType)`
- `NormalizeAsNullable<T>(value, DbDataType)`

Public way to use (recommended):

- Streaming: use `AdoAsync.Common.DataRecordExtensions.Get<T>` which already handles common conversions.
- Outputs: output parameters are normalized during extraction using the declared `DbDataType`.

Example (streaming typed read):

```csharp
using AdoAsync.Common;

await foreach (var record in executor.StreamAsync(new CommandDefinition { CommandText = "select ..." }))
{
    var id = record.Get<int>(0) ?? 0;
}
```

Example (output parameter normalization happens automatically on buffered path):

```csharp
using System.Data;

(DataTable Table, IReadOnlyDictionary<string, object?> OutputParameters) result =
    await executor.QueryTableAsync(new CommandDefinition
{
    CommandText = "dbo.ProcWithOutputs",
    CommandType = CommandType.StoredProcedure,
    Parameters = new[]
    {
        new DbParameter { Name = "@flag", DataType = DbDataType.Boolean, Direction = ParameterDirection.Output }
    }
});

// result.OutputParameters["flag"] is already normalized (ex: 0/1 -> false/true when possible)
var flag = (bool?)result.OutputParameters["flag"];
```

### `NullHandlingExtensions` (null/DBNull helper behavior)

Internal method:

- `ToNullIfDbNull(value)` → returns `null` if `value` is `DBNull`

Public way to use:

- For `DataRow`, prefer `row.Field<T?>("Col")` which already handles `DBNull`.
- For raw `object` values, do the same check explicitly.

Example:

```csharp
var raw = row["OptionalCol"];
var value = raw is DBNull ? null : raw;
```

Preferred alternatives you should use in app code:

- `DataRow`: `row.Field<T?>("OptionalCol")` (handles DBNull)
- Streaming: `record.Get<T>("OptionalCol")` (returns default for DBNull)

---

## New Extension Added in 1.5: `DbExecutorQueryExtensions.QueryAsync<T>`

File: `src/AdoAsync/Execution/DbExecutorQueryExtensions.cs`

Why it was added:

- The docs used `await foreach (var x in executor.QueryAsync(...))` for streaming mapping,
  but the library only exposed `StreamAsync(...)` on `IDbExecutor`.
- This extension provides the missing convenience wrapper: it maps `StreamAsync(...)` into an `IAsyncEnumerable<T>`.

How to use it:

```csharp
using AdoAsync.Execution;

await foreach (var item in executor.QueryAsync(
    new CommandDefinition { CommandText = "select Id from dbo.Items" },
    record => record.GetInt32(0)))
{
}
```

Notes:

- Provider support matches `StreamAsync`: SQL Server + PostgreSQL only.
- For Oracle, use buffered APIs (`QueryTableAsync`, `ExecuteDataSetAsync`).
