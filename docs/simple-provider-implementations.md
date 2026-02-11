# Simple Provider Implementations (Standalone)

This page shows a separate, minimal implementation for SQL Server, PostgreSQL, and Oracle.
It does not use `DbExecutor` or `CommandDefinitionFactory`.

Each class:

- Takes a connection string.
- Accepts a list of `SimpleParameter` values: `(name, value)` for Input, `(name, dbType, direction, size?)` for Output.
- Returns `(DataTable, OutputParameters)`.
- Uses provider-specific data adapters (no ExecuteReader).

---

## SQL Server

```csharp
using System.Data;
using AdoAsync.Simple;

using var db = new SqlServerSimpleDb("Server=.;Database=MyDb;Trusted_Connection=True;");

var parameters = new List<SimpleParameter>
{
    new("minId", 100),
    new("message", DbDataType.String, ParameterDirection.Output, 4000)
};

var result = await db.QueryTableAsync(
    commandText: "dbo.GetCustomersWithStatus",
    commandType: CommandType.StoredProcedure,
    parameters: parameters,
    commandTimeoutSeconds: 30);

DataTable table = result.Table;
string? message = (string?)result.OutputParameters["message"];
```

### SQL Server method examples (input + output)

```csharp
var scalarResult = await db.ExecuteScalarAsync<int>(
    commandText: "dbo.GetCustomerCount",
    commandType: CommandType.StoredProcedure,
    parameters: new List<SimpleParameter>
    {
        new("minId", 100),
        new("message", DbDataType.String, ParameterDirection.Output, 4000)
    });

var nonQueryResult = await db.ExecuteNonQueryAsync(
    commandText: "dbo.UpdateCustomerStatus",
    commandType: CommandType.StoredProcedure,
    parameters: new List<SimpleParameter>
    {
        new("customerId", 42),
        new("status", "Active"),
        new("message", DbDataType.String, ParameterDirection.Output, 4000)
    });

var dataSetResult = await db.ExecuteDataSetAsync(
    commandText: "dbo.GetCustomersAndOrders",
    commandType: CommandType.StoredProcedure,
    parameters: new List<SimpleParameter>
    {
        new("customerId", 42),
        new("message", DbDataType.String, ParameterDirection.Output, 4000)
    });
```

---

## PostgreSQL (refcursor example)

```csharp
using System.Data;
using AdoAsync.Simple;

using var db = new PostgreSqlSimpleDb("Host=localhost;Database=mydb;Username=myuser;Password=mypassword");

var parameters = new List<SimpleParameter>
{
    new("customer_id", 42),
    new("customer_cursor", DbDataType.RefCursor, ParameterDirection.Output),
    new("message", DbDataType.String, ParameterDirection.Output, 4000)
};

var result = await db.QueryTableAsync(
    commandText: "public.get_customer_with_status",
    commandType: CommandType.StoredProcedure,
    parameters: parameters,
    commandTimeoutSeconds: 30);

DataTable table = result.Table;
string? message = (string?)result.OutputParameters["message"];
```

### PostgreSQL method examples (input + output + refcursor)

```csharp
var scalarResult = await db.ExecuteScalarAsync<int>(
    commandText: "public.get_customer_count_with_message",
    commandType: CommandType.StoredProcedure,
    parameters: new List<SimpleParameter>
    {
        new("min_id", 100),
        new("total", DbDataType.Int32, ParameterDirection.Output),
        new("message", DbDataType.String, ParameterDirection.Output, 4000)
    });

var nonQueryResult = await db.ExecuteNonQueryAsync(
    commandText: "public.update_customer_status",
    commandType: CommandType.StoredProcedure,
    parameters: new List<SimpleParameter>
    {
        new("customer_id", 42),
        new("status", "Active"),
        new("message", DbDataType.String, ParameterDirection.Output, 4000)
    });

var dataSetResult = await db.ExecuteDataSetAsync(
    commandText: "public.get_customer_and_orders_with_status",
    commandType: CommandType.StoredProcedure,
    parameters: new List<SimpleParameter>
    {
        new("customer_id", 42),
        new("customer_cursor", DbDataType.RefCursor, ParameterDirection.Output),
        new("orders_cursor", DbDataType.RefCursor, ParameterDirection.Output),
        new("message", DbDataType.String, ParameterDirection.Output, 4000)
    });
```

### PostgreSQL procedure example (multiple refcursors -> DataSet)

```sql
CREATE OR REPLACE PROCEDURE public.get_customer_and_orders_with_status(
    IN customer_id integer,
    OUT customer_cursor refcursor,
    OUT orders_cursor refcursor,
    OUT message text
)
LANGUAGE plpgsql
AS $$
BEGIN
    OPEN customer_cursor FOR
        SELECT customer_id, name, status
        FROM public.customers
        WHERE customer_id = get_customer_and_orders_with_status.customer_id;

    OPEN orders_cursor FOR
        SELECT order_id, customer_id, total
        FROM public.orders
        WHERE customer_id = get_customer_and_orders_with_status.customer_id;

    message := 'OK';
END;
$$;
```

### PostgreSQL usage + test (DataSet from refcursors)

```csharp
var result = await db.ExecuteDataSetAsync(
    commandText: "public.get_customer_and_orders_with_status",
    commandType: CommandType.StoredProcedure,
    parameters: new List<SimpleParameter>
    {
        new("customer_id", 42),
        new("customer_cursor", DbDataType.RefCursor, ParameterDirection.Output),
        new("orders_cursor", DbDataType.RefCursor, ParameterDirection.Output),
        new("message", DbDataType.String, ParameterDirection.Output, 4000)
    });

if (result.DataSet.Tables.Count != 2)
{
    throw new InvalidOperationException("Expected 2 result tables from refcursors.");
}
```

### PostgreSQL SQL script examples (DataTable/DataSet)

```csharp
var tableResult = await db.QueryTableAsync(
    commandText: @"
        SELECT customer_id, name, status
        FROM public.customers
        WHERE customer_id >= :min_id
        ORDER BY customer_id;",
    commandType: CommandType.Text,
    parameters: new List<SimpleParameter>
    {
        new("min_id", 100)
    },
    commandTimeoutSeconds: 30);

DataTable table = tableResult.Table;

var dataSetResult = await db.ExecuteDataSetAsync(
    commandText: @"
        SELECT customer_id, name FROM public.customers WHERE customer_id >= :min_id;
        SELECT order_id, customer_id, total FROM public.orders WHERE customer_id >= :min_id;",
    commandType: CommandType.Text,
    parameters: new List<SimpleParameter>
    {
        new("min_id", 100)
    },
    commandTimeoutSeconds: 30);
```

### Convert DataTable to List (common for PostgreSQL/Oracle)

```csharp
using AdoAsync.Extensions.Execution;

var result = await db.QueryTableAsync(
    commandText: "public.get_customer_with_status",
    commandType: CommandType.StoredProcedure,
    parameters: new List<SimpleParameter>
    {
        new("customer_id", 42),
        new("customer_cursor", DbDataType.RefCursor, ParameterDirection.Output)
    });

var customers = result.Table.ToList(row => new Customer
{
    Id = row.Field<int>("customer_id"),
    Name = row.Field<string>("name") ?? string.Empty,
    Status = row.Field<string>("status") ?? string.Empty
});
```

---

## Oracle (refcursor example)

```csharp
using System.Data;
using AdoAsync.Simple;

using var db = new OracleSimpleDb("User Id=myuser;Password=mypassword;Data Source=MyOracleDb");

var parameters = new List<SimpleParameter>
{
    new("p_customer_id", 42),
    new("p_customer", DbDataType.RefCursor, ParameterDirection.Output),
    new("p_message", DbDataType.String, ParameterDirection.Output, 4000)
};

var result = await db.QueryTableAsync(
    commandText: "PKG_CUSTOMER.GET_CUSTOMER_WITH_STATUS",
    commandType: CommandType.StoredProcedure,
    parameters: parameters,
    commandTimeoutSeconds: 30);

DataTable table = result.Table;
string? message = (string?)result.OutputParameters["p_message"];
```

### Oracle method examples (input + output + refcursor)

```csharp
var scalarResult = await db.ExecuteScalarAsync<int>(
    commandText: "get_customer_count_with_message",
    commandType: CommandType.StoredProcedure,
    parameters: new List<SimpleParameter>
    {
        new("p_min_id", 100),
        new("p_total", DbDataType.Int32, ParameterDirection.Output),
        new("p_message", DbDataType.String, ParameterDirection.Output, 4000)
    });

var nonQueryResult = await db.ExecuteNonQueryAsync(
    commandText: "update_customer_status",
    commandType: CommandType.StoredProcedure,
    parameters: new List<SimpleParameter>
    {
        new("p_customer_id", 42),
        new("p_status", "Active"),
        new("p_message", DbDataType.String, ParameterDirection.Output, 4000)
    });

var dataSetResult = await db.ExecuteDataSetAsync(
    commandText: "PKG_CUSTOMER.GET_CUSTOMER_AND_ORDERS_WITH_STATUS",
    commandType: CommandType.StoredProcedure,
    parameters: new List<SimpleParameter>
    {
        new("p_customer_id", 42),
        new("p_customer", DbDataType.RefCursor, ParameterDirection.Output),
        new("p_orders", DbDataType.RefCursor, ParameterDirection.Output),
        new("p_message", DbDataType.String, ParameterDirection.Output, 4000)
    });
```

### Oracle procedure example (multiple refcursors -> DataSet)

```sql
CREATE OR REPLACE PROCEDURE PKG_CUSTOMER.GET_CUSTOMER_AND_ORDERS_WITH_STATUS(
    p_customer_id IN NUMBER,
    p_customer OUT SYS_REFCURSOR,
    p_orders OUT SYS_REFCURSOR,
    p_message OUT VARCHAR2
) AS
BEGIN
    OPEN p_customer FOR
        SELECT customer_id, name, status
        FROM customers
        WHERE customer_id = p_customer_id;

    OPEN p_orders FOR
        SELECT order_id, customer_id, total
        FROM orders
        WHERE customer_id = p_customer_id;

    p_message := 'OK';
END;
/
```

### Oracle usage + test (DataSet from refcursors)

```csharp
var result = await db.ExecuteDataSetAsync(
    commandText: "PKG_CUSTOMER.GET_CUSTOMER_AND_ORDERS_WITH_STATUS",
    commandType: CommandType.StoredProcedure,
    parameters: new List<SimpleParameter>
    {
        new("p_customer_id", 42),
        new("p_customer", DbDataType.RefCursor, ParameterDirection.Output),
        new("p_orders", DbDataType.RefCursor, ParameterDirection.Output),
        new("p_message", DbDataType.String, ParameterDirection.Output, 4000)
    });

if (result.DataSet.Tables.Count != 2)
{
    throw new InvalidOperationException("Expected 2 result tables from refcursors.");
}
```

### Oracle SQL script examples (DataTable/DataSet)

```csharp
var tableResult = await db.QueryTableAsync(
    commandText: @"
        SELECT customer_id, name, status
        FROM customers
        WHERE customer_id >= :p_min_id
        ORDER BY customer_id",
    commandType: CommandType.Text,
    parameters: new List<SimpleParameter>
    {
        new("p_min_id", 100)
    },
    commandTimeoutSeconds: 30);

DataTable table = tableResult.Table;

var dataSetResult = await db.ExecuteDataSetAsync(
    commandText: @"
        SELECT customer_id, name FROM customers WHERE customer_id >= :p_min_id;
        SELECT order_id, customer_id, total FROM orders WHERE customer_id >= :p_min_id;",
    commandType: CommandType.Text,
    parameters: new List<SimpleParameter>
    {
        new("p_min_id", 100)
    },
    commandTimeoutSeconds: 30);
```

---

## With a Transaction

```csharp
var result = await db.QueryTableInTransactionAsync(
    commandText: "dbo.UpdateCustomerStatus",
    commandType: CommandType.StoredProcedure,
    parameters: new List<SimpleParameter>
    {
        new("customerId", 42),
        new("status", "Active"),
        new("message", DbDataType.String, ParameterDirection.Output, 4000)
    });
```

---

## Notes

- For SQL Server, `@` is added automatically in these classes.
- For PostgreSQL/Oracle, prefixes are trimmed.
- If an input value is `null`, the provider may need an explicit type.
