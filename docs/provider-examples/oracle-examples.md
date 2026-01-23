# Oracle Examples

See full provider documentation: `docs/providers/oracle.md`.

---

## 1) Buffered query (recommended for Oracle)

```csharp
using System.Data;
using AdoAsync;
using AdoAsync.Execution;

await using var executor = DbExecutor.Create(new DbOptions
{
    DatabaseType = DatabaseType.Oracle,
    ConnectionString = "...",
    CommandTimeoutSeconds = 30
});

(DataTable Table, IReadOnlyDictionary<string, object?> OutputParameters) result =
    await executor.QueryTableAsync(new CommandDefinition
{
    CommandText = "select Id, Name from Customers where Id >= :min_id",
    Parameters = new[]
    {
        new DbParameter { Name = ":min_id", DataType = DbDataType.Int32, Direction = ParameterDirection.Input, Value = 100 }
    }
});

var table = result.Table;
```

---

## 2) Refcursor multi-result stored procedure (buffered)

```csharp
using System.Data;

var tables = await executor.QueryTablesAsync(new CommandDefinition
{
    CommandText = "PKG_CUSTOMER.GET_CUSTOMER_AND_ORDERS",
    CommandType = CommandType.StoredProcedure,
    Parameters = new[]
    {
        new DbParameter { Name = "p_customer_id", DataType = DbDataType.Int32, Direction = ParameterDirection.Input, Value = 42 },
        new DbParameter { Name = "p_customer", DataType = DbDataType.RefCursor, Direction = ParameterDirection.Output },
        new DbParameter { Name = "p_orders", DataType = DbDataType.RefCursor, Direction = ParameterDirection.Output }
    }
});

var customerTable = tables[0];
var ordersTable = tables[1];
```
