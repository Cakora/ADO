# PostgreSQL Examples

See full provider documentation: `docs/providers/postgresql.md`.

---

## 1) Streaming query (fast)

```csharp
using System.Data;
using AdoAsync;
using AdoAsync.Common;
using AdoAsync.Execution;

await using var executor = DbExecutor.Create(new DbOptions
{
    DatabaseType = DatabaseType.PostgreSql,
    ConnectionString = "...",
    CommandTimeoutSeconds = 30
});

await foreach (var customer in executor.QueryAsync(
    new CommandDefinition
    {
        CommandText = "select id, name from customers where id >= @min_id",
        Parameters = new[]
        {
            new DbParameter { Name = "min_id", DataType = DbDataType.Int32, Direction = ParameterDirection.Input, Value = 100 }
        }
    },
    record => new Customer
    {
        Id = record.Get<int>("id") ?? 0,
        Name = record.Get<string>("name")
    }))
{
    // process
}
```

---

## 2) Refcursor multi-result stored procedure (buffered)

```csharp
using System.Data;

var tables = await executor.QueryTablesAsync(new CommandDefinition
{
    CommandText = "public.get_customer_and_orders",
    CommandType = CommandType.StoredProcedure,
    Parameters = new[]
    {
        new DbParameter { Name = "customer_id", DataType = DbDataType.Int32, Direction = ParameterDirection.Input, Value = 42 },
        new DbParameter { Name = "customer_cursor", DataType = DbDataType.RefCursor, Direction = ParameterDirection.Output },
        new DbParameter { Name = "orders_cursor", DataType = DbDataType.RefCursor, Direction = ParameterDirection.Output }
    }
});

var customerTable = tables[0];
var ordersTable = tables[1];
```

