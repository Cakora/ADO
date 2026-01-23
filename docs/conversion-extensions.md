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

## 1) Streaming (SQL Server/PostgreSQL)

Prefer streaming when:
- you can process rows sequentially
- you want predictable low memory

### A) Recommended streaming API

Use `IDbExecutor.StreamAsync(...)` or `DbExecutorQueryExtensions.QueryAsync<T>(...)`.

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

```csharp
using AdoAsync.Extensions.Execution;

await using var result = await executor.ExecuteReaderAsync(command, cancellationToken);
await foreach (var row in result.Reader.StreamAsync(
    record => new Customer(record.GetInt32(0), record.GetString(1)),
    cancellationToken))
{
}
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

Rules:
- materialization is always explicit
- use frozen collections only for immutable, read-many lookups

## 4) Post-materialization shaping

After you already have arrays, you can project them with low overhead:

```csharp
using AdoAsync.Extensions.Execution;

var projected = customersArray.MapToArray(c => c.Id);
```

