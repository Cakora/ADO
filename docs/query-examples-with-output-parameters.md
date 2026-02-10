# Query Examples With Output Parameters

This page focuses on:

- `IDbExecutor.QueryAsync<T>(..., Func<DataRow,T> map)` → returns a buffered `List<T>` plus `OutputParameters`.
- `IDbExecutor.QueryTablesAsync(...)` → returns multiple `DataTable` results plus `OutputParameters`.

Notes:

- Output dictionary keys are normalized (prefix trimmed): `@message` / `:message` / `?message` → `"message"`.
- `ParameterDirection.ReturnValue` is treated as an output and is included in the output dictionary.
- Refcursor parameters (`DbDataType.RefCursor`) produce result tables and are not included in `OutputParameters`.
- Output string parameters should specify `Size`.

---

## SQL Server

### A) `QueryAsync<T>` (list + output message)

```csharp
using System.Data;
using AdoAsync;
using AdoAsync.Abstractions;

(List<Customer> Rows, IReadOnlyDictionary<string, object?> OutputParameters) result =
    await executor.QueryAsync(
        new CommandDefinition
        {
            CommandText = "dbo.GetCustomersWithStatus",
            CommandType = CommandType.StoredProcedure,
            Parameters = new[]
            {
                new DbParameter { Name = "@minId", DataType = DbDataType.Int32, Direction = ParameterDirection.Input, Value = 100 },
                new DbParameter { Name = "@message", DataType = DbDataType.String, Direction = ParameterDirection.Output, Size = 4000 }
            }
        },
        row => new Customer(
            Id: row.Field<int>("Id"),
            Name: row.Field<string>("Name") ?? ""));

var customers = result.Rows;
var message = (string?)result.OutputParameters["message"];
```

### B) `QueryTablesAsync` (multi-result + output message)

```csharp
using System.Data;
using AdoAsync;
using AdoAsync.Abstractions;

(IReadOnlyList<DataTable> Tables, IReadOnlyDictionary<string, object?> OutputParameters) result =
    await executor.QueryTablesAsync(new CommandDefinition
    {
        CommandText = "dbo.GetCustomerAndOrdersWithStatus",
        CommandType = CommandType.StoredProcedure,
        Parameters = new[]
        {
            new DbParameter { Name = "@customerId", DataType = DbDataType.Int32, Direction = ParameterDirection.Input, Value = 42 },
            new DbParameter { Name = "@message", DataType = DbDataType.String, Direction = ParameterDirection.Output, Size = 4000 }
        }
    });

var customerTable = result.Tables[0];
var ordersTable = result.Tables[1];
var message = (string?)result.OutputParameters["message"];
```

---

## PostgreSQL

### A) `QueryAsync<T>` (list + output message)

PostgreSQL “outputs” (OUT params) are typically used with refcursors. For a list plus an output message in one call, use a single refcursor + message:

```csharp
using System.Data;
using AdoAsync;
using AdoAsync.Abstractions;

(List<Customer> Rows, IReadOnlyDictionary<string, object?> OutputParameters) result =
    await executor.QueryAsync(
        new CommandDefinition
        {
            CommandText = "public.get_customers_with_status",
            CommandType = CommandType.StoredProcedure,
            Parameters = new[]
            {
                new DbParameter { Name = "min_id", DataType = DbDataType.Int32, Direction = ParameterDirection.Input, Value = 100 },
                new DbParameter { Name = "customer_cursor", DataType = DbDataType.RefCursor, Direction = ParameterDirection.Output },
                new DbParameter { Name = "message", DataType = DbDataType.String, Direction = ParameterDirection.Output, Size = 4000 }
            }
        },
        row => new Customer(
            Id: row.Field<int>("id"),
            Name: row.Field<string>("name") ?? ""));

var customers = result.Rows;
var message = (string?)result.OutputParameters["message"];
```

### B) `QueryTablesAsync` (refcursor multi-result + output message)

```csharp
using System.Data;
using AdoAsync;
using AdoAsync.Abstractions;

(IReadOnlyList<DataTable> Tables, IReadOnlyDictionary<string, object?> OutputParameters) result =
    await executor.QueryTablesAsync(new CommandDefinition
    {
        CommandText = "public.get_customer_and_orders_with_status",
        CommandType = CommandType.StoredProcedure,
        Parameters = new[]
        {
            new DbParameter { Name = "customer_id", DataType = DbDataType.Int32, Direction = ParameterDirection.Input, Value = 42 },
            new DbParameter { Name = "customer_cursor", DataType = DbDataType.RefCursor, Direction = ParameterDirection.Output },
            new DbParameter { Name = "orders_cursor", DataType = DbDataType.RefCursor, Direction = ParameterDirection.Output },
            new DbParameter { Name = "message", DataType = DbDataType.String, Direction = ParameterDirection.Output, Size = 4000 }
        }
    });

var customerTable = result.Tables[0];
var ordersTable = result.Tables[1];
var message = (string?)result.OutputParameters["message"];
```

---

## Oracle

### A) `QueryAsync<T>` (list + output message)

Oracle result sets are typically returned via refcursors. For a list plus an output message in one call, use a single refcursor + message:

```csharp
using System.Data;
using AdoAsync;
using AdoAsync.Abstractions;

(List<Customer> Rows, IReadOnlyDictionary<string, object?> OutputParameters) result =
    await executor.QueryAsync(
        new CommandDefinition
        {
            CommandText = "PKG_CUSTOMER.GET_CUSTOMERS_WITH_STATUS",
            CommandType = CommandType.StoredProcedure,
            Parameters = new[]
            {
                new DbParameter { Name = "p_min_id", DataType = DbDataType.Int32, Direction = ParameterDirection.Input, Value = 100 },
                new DbParameter { Name = "p_customers", DataType = DbDataType.RefCursor, Direction = ParameterDirection.Output },
                new DbParameter { Name = "p_message", DataType = DbDataType.String, Direction = ParameterDirection.Output, Size = 4000 }
            }
        },
        row => new Customer(
            Id: row.Field<int>("Id"),
            Name: row.Field<string>("Name") ?? ""));

var customers = result.Rows;
var message = (string?)result.OutputParameters["p_message"];
```

### B) `QueryTablesAsync` (refcursor multi-result + output message)

```csharp
using System.Data;
using AdoAsync;
using AdoAsync.Abstractions;

(IReadOnlyList<DataTable> Tables, IReadOnlyDictionary<string, object?> OutputParameters) result =
    await executor.QueryTablesAsync(new CommandDefinition
    {
        CommandText = "PKG_CUSTOMER.GET_CUSTOMER_AND_ORDERS_WITH_STATUS",
        CommandType = CommandType.StoredProcedure,
        Parameters = new[]
        {
            new DbParameter { Name = "p_customer_id", DataType = DbDataType.Int32, Direction = ParameterDirection.Input, Value = 42 },
            new DbParameter { Name = "p_customer", DataType = DbDataType.RefCursor, Direction = ParameterDirection.Output },
            new DbParameter { Name = "p_orders", DataType = DbDataType.RefCursor, Direction = ParameterDirection.Output },
            new DbParameter { Name = "p_message", DataType = DbDataType.String, Direction = ParameterDirection.Output, Size = 4000 }
        }
    });

var customerTable = result.Tables[0];
var ordersTable = result.Tables[1];
var message = (string?)result.OutputParameters["p_message"];
```
