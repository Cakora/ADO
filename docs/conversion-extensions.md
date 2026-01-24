# Conversion Extensions (How To Use)

This doc explains how to use the post-fetch conversion extensions (reader/table/dataset → .NET types/collections) and the rules behind them.

For design rules and enforcement, see:
- `Documentation/ConversionGuidelines.md`
- `Documentation/RulesCheatSheet.md`

## Namespaces

These extension classes live under category folders in `src/AdoAsync/Extensions/`, but keep a single discoverable namespace:

- `using AdoAsync.Extensions.Execution;`

Typed getters for streaming mapping live in:

- `using AdoAsync.Common;` (`IDataRecord.Get<T>(...)`)

## Quick combination matrix (choose by source + target)

| You have | You want | Use |
|---|---|---|
| `IDbExecutor.StreamAsync(...)` (`IAsyncEnumerable<IDataRecord>`) | one-pass processing | `await foreach` + `record.Get<T>(...)` |
| `IDbExecutor.StreamAsync(...)` | `List<T>` / `T[]` | `AsyncEnumerableMaterializerExtensions.ToListAsync` / `ToArrayAsync` |
| `IDbExecutor.StreamAsync(...)` | grouping (read-many) | `ToLookupAsync` / `ToFrozenDictionaryAsync` |
| `DbExecutorQueryExtensions.QueryAsync<T>(...)` (`IAsyncEnumerable<T>`) | one-pass processing | `await foreach` |
| `DbExecutorQueryExtensions.QueryAsync<T>(...)` | `List<T>` / `T[]` | `ToListAsync` / `ToArrayAsync` |
| `DbExecutorQueryExtensions.QueryAsync<T>(...)` | grouping (read-many) | `ToLookupAsync` / `ToFrozenDictionaryAsync` |
| `IDbExecutor.ExecuteReaderAsync(...)` (`StreamingReaderResult`) | stream raw rows | `result.Reader.StreamRecordsAsync(...)` |
| `IDbExecutor.ExecuteReaderAsync(...)` | stream mapped rows | `result.Reader.StreamAsync(map, ...)` |
| `IDbExecutor.ExecuteReaderAsync(...)` | output parameters | `await result.GetOutputParametersAsync()` (after reader is closed) |
| `IDbExecutor.QueryTableAsync(...)` (`DataTable`) | `List<T>` / `T[]` | `DataTableExtensions.ToList` / `ToArray` |
| `IDbExecutor.ExecuteDataSetAsync(...)` (`DataSet`) | `MultiResult` | `DataSetExtensions.ToMultiResult(outputs)` |
| `DataSet` or `MultiResult` | arrays/lists per table | `MultiResultMapExtensions.*` |
| `T[]` | fast projection | `SpanMappingExtensions.MapToArray(...)` |

## 1) Streaming (SQL Server/PostgreSQL)

Prefer streaming when:
- you can process rows sequentially
- you want predictable low memory

### A) Recommended streaming API

Use `IDbExecutor.StreamAsync(...)` or `DbExecutorQueryExtensions.QueryAsync<T>(...)`.

#### A1) `IDbExecutor.StreamAsync` + typed getters (recommended for low-level mapping)

```csharp
using AdoAsync.Common;
using AdoAsync.Execution;

await foreach (var record in executor.StreamAsync(
    new CommandDefinition { CommandText = "select Id, Name from dbo.Customers" },
    cancellationToken))
{
    var id = record.Get<int>(0) ?? 0;
    var name = record.Get<string>(1) ?? "";
}
```

#### A2) `DbExecutorQueryExtensions.QueryAsync<T>` (streaming + mapping convenience)

```csharp
using AdoAsync.Common;
using AdoAsync.Execution;

await foreach (var customer in executor.QueryAsync(
    new CommandDefinition { CommandText = "select Id, Name from dbo.Customers" },
    record => new Customer(record.Get<int>(0) ?? 0, record.Get<string>(1) ?? "")))
{
}
```

### B) Lowest-level reader streaming

If you use `ExecuteReaderAsync(...)` and read via `DbDataReader`, use the DataReader extensions:

#### B1) `ExecuteReaderAsync` + `DbDataReaderExtensions.StreamRecordsAsync`

```csharp
using AdoAsync.Extensions.Execution;

await using var result = await executor.ExecuteReaderAsync(command, cancellationToken);
await foreach (var record in result.Reader.StreamRecordsAsync(cancellationToken))
{
    // record is the live reader row
}
```

#### B2) `ExecuteReaderAsync` + `DbDataReaderExtensions.StreamAsync<T>` (streaming projection)

```csharp
using AdoAsync.Extensions.Execution;

await using var result = await executor.ExecuteReaderAsync(command, cancellationToken);
await foreach (var row in result.Reader.StreamAsync(
    record => new Customer(record.GetInt32(0), record.GetString(1)),
    cancellationToken))
{
}
```

#### B2.1) `ExecuteReaderAsync` + `StreamAsync<T>` + materialize (explicit decision)

```csharp
using AdoAsync.Extensions.Execution;

await using var result = await executor.ExecuteReaderAsync(command, cancellationToken);
var customers = await result.Reader
    .StreamAsync(record => new Customer(record.GetInt32(0), record.GetString(1)), cancellationToken)
    .ToListAsync(cancellationToken);
```

#### B3) `ExecuteReaderAsync` + output parameters (SQL Server/PostgreSQL only)

Output params are available only after the reader is closed:

```csharp
using System.Data;
using AdoAsync;

await using var result = await executor.ExecuteReaderAsync(new CommandDefinition
{
    CommandText = "dbo.SelectCustomersAndReturnTotal",
    CommandType = CommandType.StoredProcedure,
    Parameters = new[]
    {
        new DbParameter { Name = "@minId", DataType = DbDataType.Int32, Direction = ParameterDirection.Input, Value = 100 },
        new DbParameter { Name = "@total", DataType = DbDataType.Int32, Direction = ParameterDirection.Output }
    }
}, cancellationToken);

await foreach (var _ in result.Reader.StreamRecordsAsync(cancellationToken)) { }

var outputs = await result.GetOutputParametersAsync(cancellationToken);
var total = (int?)outputs?["total"];
```

Notes:
- Streaming must stay streaming (no hidden `.ToList()`).
- Oracle does not support streaming in this library.

## 2) Buffered (Oracle/refcursor/multi-result)

Use buffered results when:
- provider is Oracle
- you need multi-result sets
- you need to pass materialized data to other layers

### A) DataTable → `List<T>` / `T[]`

```csharp
using AdoAsync.Extensions.Execution;

var (table, outputs) = await executor.QueryTableAsync(command, cancellationToken);
try
{
    var rows = table.ToList(row => new Customer((int)row["Id"], (string)row["Name"]));
    var array = table.ToArray(row => new Customer((int)row["Id"], (string)row["Name"]));
}
finally
{
    table.Dispose();
}
```

Ownership rule:
- converters do not dispose tables; caller disposes as soon as mapping is complete.

### B) DataSet → MultiResult → mapped collections

```csharp
using AdoAsync.Extensions.Execution;

var (dataSet, outputs) = await executor.ExecuteDataSetAsync(command, cancellationToken);
try
{
    var multi = dataSet.ToMultiResult(outputs);
    var arrays = multi.MapTablesToArrays(row => new Customer((int)row["Id"], (string)row["Name"]));
}
finally
{
    dataSet.Dispose();
}
```

### C) QueryTablesAsync (buffered multi-result) + mapping

```csharp
using AdoAsync.Extensions.Execution;

var (tables, outputs) = await executor.QueryTablesAsync(command, cancellationToken);
try
{
    // Map first table to list
    var customers = tables[0].ToList(row => new Customer((int)row["Id"], (string)row["Name"]));
}
finally
{
    foreach (var t in tables) t.Dispose();
}
```

## 3) Materialize a stream (explicit decision point)

When you truly need materialization:

```csharp
using AdoAsync.Extensions.Execution;

var list = await executor.StreamAsync(command, cancellationToken).ToListAsync(cancellationToken);
var frozen = await executor.StreamAsync(command, cancellationToken)
    .ToFrozenDictionaryAsync(
        record => record.GetInt32(0),
        record => record.GetString(1),
        cancellationToken: cancellationToken);
```

### Grouping combinations (after materialization)

```csharp
using AdoAsync.Extensions.Execution;

var lookup = await executor.StreamAsync(command, cancellationToken)
    .ToLookupAsync(record => record.GetInt32(0), record => record.GetString(1), cancellationToken: cancellationToken);
```

Rules:
- materialization is always explicit
- use frozen collections only for immutable, read-many lookups

## 4) Post-materialization shaping

After you already have arrays, you can project them with low overhead:

```csharp
using AdoAsync.Extensions.Execution;

var projected = customersArray.MapToArray(c => c.Id);
```
