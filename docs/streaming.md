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

## 1.1) Provider-Specific `List<Customer>` Examples

Goal: get a `List<Customer>` with the correct API per provider.

- SQL Server / PostgreSQL: stream rows (`IAsyncEnumerable<Customer>`) then materialize via `ToListAsync(...)`.
- Oracle: streaming is not supported, so use buffered `IDbExecutor.QueryAsync<Customer>(...)`.

### SQL Server (streaming → `List<Customer>`)

```csharp
using System.Collections.Generic;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using AdoAsync;
using AdoAsync.Execution;
using AdoAsync.Extensions.Execution;

public sealed record Customer(int Id, string Name);

static async Task<List<Customer>> GetCustomersSqlServerAsync(string connectionString, int minId, CancellationToken cancellationToken)
{
    await using var executor = DbExecutor.Create(new DbOptions
    {
        DatabaseType = DatabaseType.SqlServer,
        ConnectionString = connectionString,
        CommandTimeoutSeconds = 30
    });

    return await DbExecutorQueryExtensions
        .QueryAsync<Customer>(
            executor,
            new CommandDefinition
            {
                CommandText = "select Id, Name from dbo.Customers where Id >= @minId",
                CommandType = CommandType.Text,
                Parameters = new[]
                {
                    new DbParameter { Name = "@minId", DataType = DbDataType.Int32, Direction = ParameterDirection.Input, Value = minId }
                }
            },
            map: record => new Customer(
                Id: record.GetInt32(record.GetOrdinal("Id")),
                Name: record.GetString(record.GetOrdinal("Name"))),
            cancellationToken: cancellationToken)
        .ToListAsync(cancellationToken);
}
```

SQL Server stored procedure version (still streaming):

```csharp
// Stored procedure: dbo.GetCustomers(@minId) -> resultset (Id, Name)
return await DbExecutorQueryExtensions
    .QueryAsync<Customer>(
        executor,
        new CommandDefinition
        {
            CommandText = "dbo.GetCustomers",
            CommandType = CommandType.StoredProcedure,
            Parameters = new[]
            {
                new DbParameter { Name = "@minId", DataType = DbDataType.Int32, Direction = ParameterDirection.Input, Value = minId }
            }
        },
        map: record => new Customer(
            Id: record.GetInt32(record.GetOrdinal("Id")),
            Name: record.GetString(record.GetOrdinal("Name"))),
        cancellationToken: cancellationToken)
    .ToListAsync(cancellationToken);
```

### PostgreSQL (streaming → `List<Customer>`)

```csharp
using System.Collections.Generic;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using AdoAsync;
using AdoAsync.Execution;
using AdoAsync.Extensions.Execution;

public sealed record Customer(int Id, string Name);

static async Task<List<Customer>> GetCustomersPostgreSqlAsync(string connectionString, int minId, CancellationToken cancellationToken)
{
    await using var executor = DbExecutor.Create(new DbOptions
    {
        DatabaseType = DatabaseType.PostgreSql,
        ConnectionString = connectionString,
        CommandTimeoutSeconds = 30
    });

    return await DbExecutorQueryExtensions
        .QueryAsync<Customer>(
            executor,
            new CommandDefinition
            {
                CommandText = "select id, name from public.customers where id >= @min_id",
                CommandType = CommandType.Text,
                Parameters = new[]
                {
                    new DbParameter { Name = "@min_id", DataType = DbDataType.Int32, Direction = ParameterDirection.Input, Value = minId }
                }
            },
            map: record => new Customer(
                Id: record.GetInt32(record.GetOrdinal("id")),
                Name: record.GetString(record.GetOrdinal("name"))),
            cancellationToken: cancellationToken)
        .ToListAsync(cancellationToken);
}
```

PostgreSQL stored procedure version (streaming only if it returns a rowset directly):

```csharp
// Stored procedure: public.get_customers(min_id) -> rowset (id, name)
return await DbExecutorQueryExtensions
    .QueryAsync<Customer>(
        executor,
        new CommandDefinition
        {
            CommandText = "public.get_customers",
            CommandType = CommandType.StoredProcedure,
            Parameters = new[]
            {
                new DbParameter { Name = "min_id", DataType = DbDataType.Int32, Direction = ParameterDirection.Input, Value = minId }
            }
        },
        map: record => new Customer(
            Id: record.GetInt32(record.GetOrdinal("id")),
            Name: record.GetString(record.GetOrdinal("name"))),
        cancellationToken: cancellationToken)
    .ToListAsync(cancellationToken);
```

If your PostgreSQL procedure returns results via `refcursor`, use the buffered refcursor pattern (`QueryTablesAsync` / `ExecuteDataSetAsync`) instead of streaming.

### Oracle (buffered → `List<Customer>`)

```csharp
using System;
using System.Collections.Generic;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using AdoAsync;
using AdoAsync.Execution;

public sealed record Customer(int Id, string Name);

static async Task<List<Customer>> GetCustomersOracleAsync(string connectionString, int minId, CancellationToken cancellationToken)
{
    await using var executor = DbExecutor.Create(new DbOptions
    {
        DatabaseType = DatabaseType.Oracle,
        ConnectionString = connectionString,
        CommandTimeoutSeconds = 30
    });

    (List<Customer> rows, _) = await executor.QueryAsync<Customer>(
        new CommandDefinition
        {
            CommandText = "select ID, NAME from CUSTOMERS where ID >= :min_id",
            CommandType = CommandType.Text,
            Parameters = new[]
            {
                new DbParameter { Name = ":min_id", DataType = DbDataType.Int32, Direction = ParameterDirection.Input, Value = minId }
            }
        },
        map: row => new Customer(
            Id: Convert.ToInt32(row["ID"]),
            Name: Convert.ToString(row["NAME"]) ?? string.Empty),
        cancellationToken: cancellationToken);

    return rows;
}
```

Oracle stored procedure version (buffered refcursor):

```csharp
// Stored procedure: PKG_CUSTOMER.GET_CUSTOMERS(p_min_id IN number, p_customers OUT sys_refcursor)
var tables = await executor.QueryTablesAsync(new CommandDefinition
{
    CommandText = "PKG_CUSTOMER.GET_CUSTOMERS",
    CommandType = CommandType.StoredProcedure,
    Parameters = new[]
    {
        new DbParameter { Name = "p_min_id", DataType = DbDataType.Int32, Direction = ParameterDirection.Input, Value = minId },
        new DbParameter { Name = "p_customers", DataType = DbDataType.RefCursor, Direction = ParameterDirection.Output }
    }
}, cancellationToken);

var customersTable = tables.Tables[0];
var customers = new List<Customer>(customersTable.Rows.Count);
foreach (DataRow row in customersTable.Rows)
{
    customers.Add(new Customer(
        Id: Convert.ToInt32(row["ID"]),
        Name: Convert.ToString(row["NAME"]) ?? string.Empty));
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

Materialize to `List<Customer>` (SQL Server/PostgreSQL only):

```csharp
using System.Collections.Generic;
using System.Data;
using System.Threading;
using AdoAsync;
using AdoAsync.Execution;
using AdoAsync.Extensions.Execution;

public sealed record Customer(int Id, string Name);

// IAsyncEnumerable<Customer> -> List<Customer>
List<Customer> customers = await DbExecutorQueryExtensions
    .QueryAsync<Customer>(
        executor,
        new CommandDefinition
        {
            CommandText = "select Id, Name from dbo.Customers where Id >= @minId",
            CommandType = CommandType.Text,
            Parameters = new[]
            {
                new DbParameter { Name = "@minId", DataType = DbDataType.Int32, Direction = ParameterDirection.Input, Value = 100 }
            }
        },
        map: record => new Customer(
            Id: record.GetInt32(record.GetOrdinal("Id")),
            Name: record.GetString(record.GetOrdinal("Name"))),
        cancellationToken: cancellationToken)
    .ToListAsync(cancellationToken);
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
        CommandText = "select ID, NAME from CUSTOMERS where ID >= :min_id",
        CommandType = CommandType.Text,
        Parameters = new[]
        {
            new DbParameter { Name = ":min_id", DataType = DbDataType.Int32, Direction = ParameterDirection.Input, Value = 100 }
        }
    },
    map: row => new Customer(
        Id: Convert.ToInt32(row["ID"]),
        Name: Convert.ToString(row["NAME"]) ?? string.Empty),
    cancellationToken: cancellationToken);

List<Customer> customers = result.Rows;
```
