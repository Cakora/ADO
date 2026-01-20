# AdoAsync

Async-first ADO.NET helper that keeps provider logic contained for SQL Server, PostgreSQL, and Oracle. It enforces validation, explicit transactions, optional Polly retries, and returns a custom error contract instead of leaking provider exceptions.

## Features
- Async-only API surface for commands, scalars, streaming queries, and multi-result sets.
- FluentValidation-backed options/command/parameter checks (opt-in via `EnableValidation`).
- Retry policies (Polly) that are off by default and skip retries inside user transactions.
- Provider-specific type and exception mappers kept in dedicated namespaces.
- Explicit transaction handles (`IAsyncDisposable`) with rollback-on-dispose semantics.
- Diagnostics off by default; returned via result contracts instead of logging.
- Explicit bulk import with column mapping (provider-owned implementations).

## Errors and Exceptions
- Prefer `DbResult.Error` where available (no thrown provider exceptions).
- When exceptions are thrown, catch `DbCallerException` and read `DbCallerException.Error`.
- `DbClientException` exists only as a legacy name; prefer `DbCallerException`.

## Quick Start
```csharp
var options = new DbOptions
{
    DatabaseType = DatabaseType.SqlServer,
    ConnectionString = "...",
    CommandTimeoutSeconds = 30,
    EnableValidation = true,
    EnableRetry = false
};

await using var executor = DbExecutor.Create(options);

var rows = await executor.ExecuteAsync(new CommandDefinition(
    "update dbo.Items set Processed = 1 where Id = @id",
    new[] { new DbParameter("id", DbDataType.Int32, 42) }));
```

See `IMPLEMENTATION_GUIDE.md` for the full behavior and guardrails.

You can also supply a provider `DbDataSource` via `DbOptions.DataSource` to reuse configured data sources when supported by your provider.

## Dependency Injection (Multi-DB)
`DbExecutor` is not thread-safe, so register it as `Scoped` and use a factory for multiple databases.

If options change per call, register only the factory and create executors from per-call options:
```csharp
using AdoAsync.Abstractions;
using AdoAsync.DependencyInjection;

builder.Services.AddAdoAsyncFactory();

// Your app decides per-call values (timeout/retry/connection string/tenant, etc.)
var baseOptions = new DbOptions { DatabaseType = DatabaseType.SqlServer, ConnectionString = "...", CommandTimeoutSeconds = 30 };
builder.Services.AddSingleton(baseOptions);
```

```csharp
public sealed class MyService(IDbExecutorFactory factory, DbOptions baseOptions)
{
    public async Task RunAsync(bool enableRetry, int timeoutSeconds)
    {
        var options = baseOptions with { EnableRetry = enableRetry, CommandTimeoutSeconds = timeoutSeconds };
        await using var exec = factory.Create(options);
        await exec.ExecuteAsync(new CommandDefinition("select 1"));
    }
}
```

Enum keys are supported for convenience (stored internally as names via `ToString()`):
```csharp
using AdoAsync.Abstractions;
using AdoAsync.DependencyInjection;

public enum DbKey { Main, Reporting }

builder.Services.AddAdoAsync(DbKey.Main, new DbOptions { DatabaseType = DatabaseType.SqlServer, ConnectionString = "...", CommandTimeoutSeconds = 30 });
builder.Services.AddAdoAsync(DbKey.Reporting, new DbOptions { DatabaseType = DatabaseType.PostgreSql, ConnectionString = "...", CommandTimeoutSeconds = 30 });

var app = builder.Build();

// Resolve on demand
var factory = app.Services.GetRequiredService<IDbExecutorFactory>();
await using var reporting = factory.Create(DbKey.Reporting);
```

## Read Patterns (3 ways)

### 1) Simple Reader (ADO.NET)
This is the classic `ExecuteReader` pattern (direct ADO.NET, not via `DbExecutor`):
```csharp
using System.Data;
using System.Data.Common;

await using DbConnection conn = new Microsoft.Data.SqlClient.SqlConnection(options.ConnectionString);
await conn.OpenAsync();

await using var cmd = conn.CreateCommand();
cmd.CommandText = "select Id, Name from dbo.Customers";
cmd.CommandType = CommandType.Text;

await using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.Default);
while (await reader.ReadAsync())
{
    var id = reader.GetInt64(0);
    var name = reader.GetString(1);
}
```

### 2) Streaming (AdoAsync)
Best for high performance and low memory (row-by-row):
```csharp
using System.Data;
using AdoAsync.Common;

await foreach (var customer in executor.QueryAsync(
    new CommandDefinition
    {
        CommandText = "select Id, Name from dbo.Customers",
        CommandType = CommandType.Text
    },
    record => new Customer
    {
        Id = record.Get<long>(0),
        Name = record.Get<string>(1)
    }))
{
    // process customer
}
```

### 3) DataTable (AdoAsync)
Easy, but buffers all rows (materialized):
```csharp
using System.Data;
using System.Linq;

var result = await executor.QueryTablesAsync(new CommandDefinition
{
    CommandText = "select Id, Name from dbo.Customers",
    CommandType = CommandType.Text
});

var table = result.Tables![0];
foreach (DataRow row in table.Rows)
{
    var id = (long)row["Id"];
    var name = (string)row["Name"];
}
```

## Bulk Import (Optional)
```csharp
using var reader = GetSourceReader();
var request = new BulkImportRequest
{
    DestinationTable = "dbo.Items",
    SourceReader = reader,
    ColumnMappings = new[]
    {
        new BulkImportColumnMapping { SourceColumn = "Id", DestinationColumn = "Id" },
        new BulkImportColumnMapping { SourceColumn = "Name", DestinationColumn = "Name" }
    },
    AllowedDestinationTables = new HashSet<string> { "dbo.Items" },
    AllowedDestinationColumns = new HashSet<string> { "Id", "Name" }
};

var result = await executor.BulkImportAsync(request);
```

## Transactions
All commands and bulk imports enlist in an explicit executor transaction when started via `BeginTransactionAsync`.
If any operation fails, disposing the transaction handle without committing will roll back everything.

```csharp
await using var executor = DbExecutor.Create(options);

await using var tx = await executor.BeginTransactionAsync();

var first = await executor.BulkImportAsync(firstRequest);
if (!first.Success) throw new Exception(first.Error!.MessageKey);

var second = await executor.BulkImportAsync(secondRequest);
if (!second.Success) throw new Exception(second.Error!.MessageKey);

await tx.CommitAsync();
```

## Result Shapes (Single + Multi-Result)
Single SELECT or stored procedure -> `QueryTablesAsync` returns one `DataTable`:
```csharp
var result = await executor.QueryTablesAsync(new CommandDefinition
{
    CommandText = "select Id, Name from dbo.Customers",
    CommandType = CommandType.Text
});

var table = result.Tables![0];
```

## Multi-Result (All Providers)
`QueryTablesAsync` supports multiple result sets, but the mechanism differs per provider:

### SQL Server (multiple result sets)
SQL Server can return multiple result sets from a stored procedure (or multiple `SELECT` statements in one batch):
```csharp
var result = await executor.QueryTablesAsync(new CommandDefinition
{
    CommandText = "dbo.GetCustomerAndOrders",
    CommandType = CommandType.StoredProcedure,
    Parameters = new[]
    {
        new DbParameter { Name = "@customerId", DataType = DbDataType.Int32, Value = 42 }
    }
});

var customerTable = result.Tables![0];
var ordersTable = result.Tables![1];
```

### PostgreSQL (refcursor outputs)
PostgreSQL stored procedures can return multiple refcursors via output parameters:
```csharp
var result = await executor.QueryTablesAsync(new CommandDefinition
{
    CommandText = "public.get_customer_and_orders",
    CommandType = CommandType.StoredProcedure,
    Parameters = new[]
    {
        new DbParameter { Name = "customer_id", DataType = DbDataType.Int32, Value = 42, Direction = ParameterDirection.Input },
        new DbParameter { Name = "customer_cursor", DataType = DbDataType.RefCursor, Direction = ParameterDirection.Output },
        new DbParameter { Name = "orders_cursor", DataType = DbDataType.RefCursor, Direction = ParameterDirection.Output }
    }
});

var customerTable = result.Tables![0];
var ordersTable = result.Tables![1];
```

### Oracle (RefCursor outputs)
Oracle returns multi-result sets via `RefCursor` output parameters:
```csharp
var result = await executor.QueryTablesAsync(new CommandDefinition
{
    CommandText = "PKG_CUSTOMER.GET_CUSTOMER_AND_ORDERS",
    CommandType = CommandType.StoredProcedure,
    Parameters = new[]
    {
        new DbParameter { Name = "p_customer_id", DataType = DbDataType.Int32, Value = 42, Direction = ParameterDirection.Input },
        new DbParameter { Name = "p_customer", DataType = DbDataType.RefCursor, Direction = ParameterDirection.Output },
        new DbParameter { Name = "p_orders", DataType = DbDataType.RefCursor, Direction = ParameterDirection.Output }
    }
});

var customerTable = result.Tables![0];
var ordersTable = result.Tables![1];
```

Multi-result stored procedure -> `QueryTablesAsync` returns multiple tables:
```csharp
var result = await executor.QueryTablesAsync(new CommandDefinition
{
    CommandText = "dbo.GetCustomerAndOrders",
    CommandType = CommandType.StoredProcedure,
    Parameters = new[]
    {
        new DbParameter
        {
            Name = "@customerId",
            DataType = DbDataType.Int32,
            Direction = ParameterDirection.Input,
            Value = 42
        }
    }
});

var customerTable = result.Tables![0];
var ordersTable = result.Tables![1];
```

Multi-result -> DataSet:
```csharp
var dataSet = new DataSet();
foreach (var table in result.Tables!)
{
    dataSet.Tables.Add(table);
}
```

Explicit mapping to custom classes (fast, no reflection):
```csharp
using AdoAsync.Common;

await foreach (var customer in executor.QueryAsync(new CommandDefinition
{
    CommandText = "select Id, Name from dbo.Customers",
    CommandType = CommandType.Text
}, record => new Customer
{
    Id = record.Get<long>(0),
    Name = record.Get<string>(1)
}))
{
    // use customer
}
```

Custom class mapping from DataTable (multi-result example):
```csharp
var customers = result.Tables![0]
    .AsEnumerable()
    .Select(row => new Customer
    {
        Id = row.Field<long>("Id"),
        Name = row.Field<string>("Name")
    })
    .ToArray();
```

Provider notes:
- SQL Server: multi-result stored procedures are supported natively.
- Oracle: multi-result stored procedures use `RefCursor` output parameters.
- PostgreSQL: multi-result stored procedures use `refcursor` outputs; multi-SELECT SQL text is also supported.

Provider-specific examples:
- `docs/provider-examples/sqlserver-examples.md`
- `docs/provider-examples/postgresql-examples.md`
- `docs/provider-examples/oracle-examples.md`
- `docs/type-handling.md` (provider type differences and normalization notes)

## Integration Tests (Run Later)
The integration tests are skipped by default because they require a live database.
To run them later:
1) Set `ADOASYNC_TEST_CONNECTION` to a real connection string.
2) Create stored procedures matching the names in `tests/AdoAsync.Tests/QueryTablesIntegrationTests.cs`.
3) Remove the `[Skip = ...]` attributes in `tests/AdoAsync.Tests/QueryTablesIntegrationTests.cs`.
