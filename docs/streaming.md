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

## 1.1) Unified `List<Customer>` Example (All Providers)

Goal: keep the example shape as identical as possible. Only the command differs by provider (streaming for SQL Server/PostgreSQL; buffered for Oracle).

```csharp
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using AdoAsync;
using AdoAsync.Common;
using AdoAsync.Abstractions;
using AdoAsync.Execution;
using AdoAsync.Extensions.Execution;

public sealed record Customer(int Id, string Name);

public static class CustomerQueries
{
    public static async Task<List<Customer>> GetCustomersAsync(DbOptions options, int minId, CancellationToken cancellationToken = default)
    {
        await using var executor = DbExecutor.Create(options);

        return options.DatabaseType switch
        {
            DatabaseType.Oracle => await GetOracleBufferedAsync(executor, minId, cancellationToken),
            _ => await GetStreamingAsync(executor, options.DatabaseType, minId, cancellationToken)
        };
    }

    private static async Task<List<Customer>> GetStreamingAsync(
        IDbExecutor executor,
        DatabaseType databaseType,
        int minId,
        CancellationToken cancellationToken)
    {
        // SQL Server + PostgreSQL streaming. Alias columns so mapping is identical.
        // (PostgreSQL also commonly uses "min_id" without a prefix, but "@minId" works.)
        var commandText = databaseType == DatabaseType.SqlServer
            ? "select Id = Id, Name = Name from dbo.Customers where Id >= @minId"
            : "select id as Id, name as Name from public.customers where id >= @minId";

        return await executor
            .QueryAsync(
                new CommandDefinition
                {
                    CommandText = commandText,
                    CommandType = CommandType.Text,
                    Parameters = new[]
                    {
                        new DbParameter { Name = "@minId", DataType = DbDataType.Int32, Direction = ParameterDirection.Input, Value = minId }
                    }
                },
                map: (IDataRecord record) => new Customer(
                    Id: record.Get<int>("Id") ?? 0,
                    Name: record.Get<string>("Name") ?? string.Empty),
                cancellationToken: cancellationToken)
            .ToListAsync(cancellationToken);
    }

    private static async Task<List<Customer>> GetOracleBufferedAsync(
        IDbExecutor executor,
        int minId,
        CancellationToken cancellationToken)
    {
        // Oracle does not support streaming in AdoAsync; use buffered QueryAsync<T> (DataTable -> List<T>).
        (List<Customer> rows, _) = await executor.QueryAsync(
            new CommandDefinition
            {
                CommandText = "select ID as Id, NAME as Name from CUSTOMERS where ID >= :minId",
                CommandType = CommandType.Text,
                Parameters = new[]
                {
                    new DbParameter { Name = ":minId", DataType = DbDataType.Int32, Direction = ParameterDirection.Input, Value = minId }
                }
            },
            map: row => new Customer(
                Id: row.Field<int>("Id"),
                Name: row.Field<string>("Name") ?? string.Empty),
            cancellationToken: cancellationToken);

        return rows;
    }
}
```

## 1.2) `List<Customer>` Example (Base + Only Differences Per Provider)

Use this when you want one “common” snippet and then only list the parts that change per provider.

### 1.2.1 Common code (same for all)

```csharp
using System.Collections.Generic;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using AdoAsync;
using AdoAsync.Common;
using AdoAsync.Abstractions;
using AdoAsync.Execution;
using AdoAsync.Extensions.Execution;

public sealed record Customer(int Id, string Name);

public static class CustomerRepository
{
    public static async Task<List<Customer>> GetCustomersAsync(DbOptions options, int minId, CancellationToken cancellationToken = default)
    {
        await using var executor = DbExecutor.Create(options);

        if (options.DatabaseType == DatabaseType.Oracle)
        {
            return await QueryOracleBufferedAsync(executor, minId, cancellationToken);
        }

        return await QueryStreamingAsync(executor, options.DatabaseType, minId, cancellationToken);
    }

    private static async Task<List<Customer>> QueryStreamingAsync(
        IDbExecutor executor,
        DatabaseType databaseType,
        int minId,
        CancellationToken cancellationToken)
    {
        return await executor
            .QueryAsync(
                new CommandDefinition
                {
                    CommandText = GetCommandText(databaseType),
                    CommandType = CommandType.Text,
                    Parameters = new[] { GetMinIdParameter(databaseType, minId) }
                },
                map: (IDataRecord record) => new Customer(
                    Id: record.Get<int>("Id") ?? 0,
                    Name: record.Get<string>("Name") ?? string.Empty),
                cancellationToken: cancellationToken)
            .ToListAsync(cancellationToken);
    }

    private static async Task<List<Customer>> QueryOracleBufferedAsync(
        IDbExecutor executor,
        int minId,
        CancellationToken cancellationToken)
    {
        (List<Customer> rows, _) = await executor.QueryAsync(
            new CommandDefinition
            {
                CommandText = GetCommandText(DatabaseType.Oracle),
                CommandType = CommandType.Text,
                Parameters = new[] { GetMinIdParameter(DatabaseType.Oracle, minId) }
            },
            map: row => new Customer(
                Id: row.Field<int>("Id"),
                Name: row.Field<string>("Name") ?? string.Empty),
            cancellationToken: cancellationToken);

        return rows;
    }

    // provider-specific parts below
    private static string GetCommandText(DatabaseType databaseType) => throw new NotImplementedException();
    private static DbParameter GetMinIdParameter(DatabaseType databaseType, int minId) => throw new NotImplementedException();
}
```

### 1.2.2 Provider-specific parts (only the differences)

SQL Server:

```csharp
private static string GetCommandText(DatabaseType databaseType) =>
    "select Id = Id, Name = Name from dbo.Customers where Id >= @minId";

private static DbParameter GetMinIdParameter(DatabaseType databaseType, int minId) =>
    new() { Name = "@minId", DataType = DbDataType.Int32, Direction = ParameterDirection.Input, Value = minId };
```

PostgreSQL:

```csharp
private static string GetCommandText(DatabaseType databaseType) =>
    "select id as Id, name as Name from public.customers where id >= @minId";

private static DbParameter GetMinIdParameter(DatabaseType databaseType, int minId) =>
    new() { Name = "@minId", DataType = DbDataType.Int32, Direction = ParameterDirection.Input, Value = minId };
```

Oracle:

```csharp
private static string GetCommandText(DatabaseType databaseType) =>
    "select ID as Id, NAME as Name from CUSTOMERS where ID >= :minId";

private static DbParameter GetMinIdParameter(DatabaseType databaseType, int minId) =>
    new() { Name = ":minId", DataType = DbDataType.Int32, Direction = ParameterDirection.Input, Value = minId };
```

## 1.3) `IAsyncEnumerable<Customer>` Example (Streaming)

This returns an `IAsyncEnumerable<Customer>` for SQL Server/PostgreSQL. Oracle streaming is not supported, so this method throws for Oracle.

```csharp
using System;
using System.Collections.Generic;
using System.Data;
using System.Threading;
using AdoAsync;
using AdoAsync.Common;
using AdoAsync.Abstractions;

public sealed record Customer(int Id, string Name);

public static class CustomerStreams
{
    public static IAsyncEnumerable<Customer> StreamCustomers(
        IDbExecutor executor,
        DatabaseType databaseType,
        int minId,
        CancellationToken cancellationToken = default)
    {
        if (databaseType == DatabaseType.Oracle)
        {
            throw new NotSupportedException("Oracle streaming is not supported; use buffered QueryAsync/QueryTableAsync instead.");
        }

        var commandText = databaseType == DatabaseType.SqlServer
            ? "select Id = Id, Name = Name from dbo.Customers where Id >= @minId"
            : "select id as Id, name as Name from public.customers where id >= @minId";

        return executor.QueryAsync(
            new CommandDefinition
            {
                CommandText = commandText,
                CommandType = CommandType.Text,
                Parameters = new[]
                {
                    new DbParameter { Name = "@minId", DataType = DbDataType.Int32, Direction = ParameterDirection.Input, Value = minId }
                }
            },
            map: (IDataRecord record) => new Customer(
                Id: record.Get<int>("Id") ?? 0,
                Name: record.Get<string>("Name") ?? string.Empty),
            cancellationToken: cancellationToken);
    }
}

// usage
await foreach (var customer in CustomerStreams.StreamCustomers(executor, options.DatabaseType, minId: 100, cancellationToken))
{
    // process customer
}
```

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
            (IDataRecord record) => new Customer(
                record.Get<int>("Id") ?? 0,
                record.Get<string>("Name") ?? string.Empty),
            cancellationToken))
        {
            Console.WriteLine($"{customer.Id} - {customer.Name}");
        }
    }
}
```

Same query, but return `List<Customer>` (SQL Server/PostgreSQL only):

```csharp
using System.Collections.Generic;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using AdoAsync;
using AdoAsync.Common;
using AdoAsync.Execution;
using AdoAsync.Extensions.Execution;

public sealed record Customer(int Id, string Name);

static async Task<List<Customer>> GetCustomersAsync(AdoAsync.Abstractions.IDbExecutor executor, CancellationToken cancellationToken)
{
    var command = new CommandDefinition
    {
        CommandText = "select Id, Name from dbo.Customers",
        CommandType = CommandType.Text
    };

    // Style A: extension method call site (recommended)
    return await executor
        .QueryAsync(
            command,
            (IDataRecord record) => new Customer(
                record.Get<int>("Id") ?? 0,
                record.Get<string>("Name") ?? string.Empty),
            cancellationToken)
        .ToListAsync(cancellationToken);
}
```

Equivalent, but calling the extension method explicitly (same behavior, just more verbose):

```csharp
// Style B: explicit static call (exact same method as Style A)
return await AdoAsync.Execution.DbExecutorQueryExtensions
    .QueryAsync(
        executor,
        command,
        (IDataRecord record) => new Customer(
            record.Get<int>("Id") ?? 0,
            record.Get<string>("Name") ?? string.Empty),
        cancellationToken)
    .ToListAsync(cancellationToken);
```

Oracle equivalent (buffered; streaming is not supported):

```csharp
using System.Collections.Generic;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using AdoAsync;
using AdoAsync.Execution;

public sealed record Customer(int Id, string Name);

static async Task<List<Customer>> GetCustomersOracleAsync(AdoAsync.Abstractions.IDbExecutor executor, CancellationToken cancellationToken)
{
    (List<Customer> rows, _) = await executor.QueryAsync(
        new CommandDefinition
        {
            CommandText = "select ID as Id, NAME as Name from CUSTOMERS",
            CommandType = CommandType.Text
        },
        map: row => new Customer(
            Id: row.Field<int>("Id"),
            Name: row.Field<string>("Name") ?? string.Empty),
        cancellationToken: cancellationToken);

    return rows;
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

Oracle example (`List<Customer>` via buffered `IDbExecutor.QueryAsync<Customer>`) :

```csharp
using System;
using System.Collections.Generic;
using System.Data;
using AdoAsync;
using AdoAsync.Abstractions;

public sealed record Customer(int Id, string Name);

(List<Customer> Rows, _) result = await executor.QueryAsync<Customer>(
    new CommandDefinition
    {
        CommandText = "select ID as Id, NAME as Name from CUSTOMERS where ID >= :minId",
        CommandType = CommandType.Text,
        Parameters = new[]
        {
            new DbParameter { Name = ":minId", DataType = DbDataType.Int32, Direction = ParameterDirection.Input, Value = 100 }
        }
    },
    map: row => new Customer(
        Id: row.Field<int>("Id"),
        Name: row.Field<string>("Name") ?? string.Empty),
    cancellationToken: cancellationToken);

List<Customer> customers = result.Rows;
```
