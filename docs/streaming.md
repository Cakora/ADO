# Streaming (IAsyncEnumerable) Guide

This guide explains the streaming APIs in AdoAsync and when to use them.

---

## 1) Provider Support

Streaming is supported for:

- SQL Server
- PostgreSQL

Streaming is **not supported** for:

- Oracle (use buffered APIs instead)

---

## 2) Streaming APIs (What To Use)

### A) Best convenience API: `QueryAsync<T>` (stream + map)

Method:

- `IAsyncEnumerable<T> DbExecutorQueryExtensions.QueryAsync<T>(...)`
- File: `src/AdoAsync/Execution/DbExecutorQueryExtensions.cs:17`

Use when:

- You want row-by-row processing
- You want typed mapping
- You do not need output parameters

Complete example:

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

### B) Lowest-level streaming: `ExecuteReaderAsync`

Method:

- `ValueTask<StreamingReaderResult> IDbExecutor.ExecuteReaderAsync(...)`

Use when:

- You want maximum control over `DbDataReader`
- You are fine with manual mapping

```csharp
await using var result = await executor.ExecuteReaderAsync(new CommandDefinition
{
    CommandText = "select Id, Name from dbo.Customers",
    CommandType = CommandType.Text
});

while (await result.Reader.ReadAsync(cancellationToken))
{
    var id = result.Reader.GetInt32(0);
    var name = result.Reader.GetString(1);
}
```

### C) Streaming + output parameters: `ExecuteReaderAsync`

Method:

- `ValueTask<StreamingReaderResult> IDbExecutor.ExecuteReaderAsync(...)`

Important rule:

- Output parameters are only available **after the reader is closed**, so you must finish/close the reader first.

```csharp
await using var result = await executor.ExecuteReaderAsync(command, cancellationToken);

await using (result.Reader)
{
    while (await result.Reader.ReadAsync(cancellationToken))
    {
        // stream rows
    }
}

var outputs = await result.GetOutputParametersAsync(cancellationToken);
```

---

## 3) When NOT To Use Streaming

Do not use streaming when:

- Provider is Oracle
- You need output parameters without waiting for reader completion (use buffered)
- You need multi-result sets (use `ExecuteDataSetAsync` / `QueryTablesAsync`)
- PostgreSQL/Oracle procedures return results via refcursor (use buffered)

---

## 4) Buffered Alternatives (When You Need Outputs)

- Output parameters (reliable): use the tuple-returning buffered methods (`QueryTableAsync` / `ExecuteDataSetAsync` / `ExecuteAsync` / `ExecuteScalarAsync<T>`)
- Multi-result + outputs: prefer `ExecuteDataSetAsync`
