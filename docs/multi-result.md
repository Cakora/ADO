# Multi-Result Patterns (All Providers)

Use these patterns when a command returns multiple result sets. Approaches differ by provider; the recommended path is to use `QueryTablesAsync` and map `DataTable` rows to your types.

## SQL Server (multiple SELECTs / stored procedures)
```csharp
var result = await executor.QueryTablesAsync(new CommandDefinition
{
    CommandText = "dbo.GetCustomerAndOrders", // or a batch with multiple SELECT statements
    CommandType = CommandType.StoredProcedure,
    Parameters = new[]
    {
        new DbParameter("customerId", DbDataType.Int32, value: 42, direction: ParameterDirection.Input)
    }
});

if (!result.Success) throw new DbCallerException(result.Error!);

var customers = result.Tables![0]
    .AsEnumerable()
    .Select(r => new Customer { Id = r.Field<int>("Id"), Name = r.Field<string>("Name") })
    .ToList();

var orders = result.Tables![1]
    .AsEnumerable()
    .Select(r => new Order { Id = r.Field<int>("Id"), Amount = r.Field<decimal>("Amount") })
    .ToList();
```

## PostgreSQL (refcursor outputs)
Stored procedures return multiple `refcursor` OUT parameters. Use `QueryTablesAsync`; it consumes the cursors and returns one `DataTable` per cursor.
```csharp
var result = await executor.QueryTablesAsync(new CommandDefinition
{
    CommandText = "public.get_customer_and_orders",
    CommandType = CommandType.StoredProcedure,
    Parameters = new[]
    {
        new DbParameter("customer_id", DbDataType.Int32, value: 42, direction: ParameterDirection.Input),
        new DbParameter("customer_cursor", DbDataType.RefCursor, direction: ParameterDirection.Output),
        new DbParameter("orders_cursor", DbDataType.RefCursor, direction: ParameterDirection.Output)
    }
});

if (!result.Success) throw new DbCallerException(result.Error!);

var customers = result.Tables![0]
    .AsEnumerable()
    .Select(r => new Customer { Id = r.Field<int>("Id"), Name = r.Field<string>("Name") })
    .ToList();

var orders = result.Tables![1]
    .AsEnumerable()
    .Select(r => new Order { Id = r.Field<int>("Id"), Amount = r.Field<decimal>("Amount") })
    .ToList();
```

## Oracle (RefCursor outputs)
Stored procedures return multiple `RefCursor` OUT parameters. Use `QueryTablesAsync`; it consumes the cursors and returns one `DataTable` per cursor.
```csharp
var result = await executor.QueryTablesAsync(new CommandDefinition
{
    CommandText = "PKG_CUSTOMER.GET_CUSTOMER_AND_ORDERS",
    CommandType = CommandType.StoredProcedure,
    Parameters = new[]
    {
        new DbParameter("p_customer_id", DbDataType.Int32, value: 42, direction: ParameterDirection.Input),
        new DbParameter("p_customer", DbDataType.RefCursor, direction: ParameterDirection.Output),
        new DbParameter("p_orders", DbDataType.RefCursor, direction: ParameterDirection.Output)
    }
});

if (!result.Success) throw new DbCallerException(result.Error!);

var customers = result.Tables![0]
    .AsEnumerable()
    .Select(r => new Customer { Id = r.Field<int>("Id"), Name = r.Field<string>("Name") })
    .ToList();

var orders = result.Tables![1]
    .AsEnumerable()
    .Select(r => new Order { Id = r.Field<int>("Id"), Amount = r.Field<decimal>("Amount") })
    .ToList();
```

## Streaming vs buffered (when to use)
- Use `QueryTablesAsync` when:
  - You are on PostgreSQL/Oracle and your proc returns refcursors (required).
  - You want `DataTable` output for binding/inspection.
- Use `QueryMultiAsync` when:
  - You want non-buffered streaming across result sets (e.g., SQL Server multi-SELECT/proc).
  - You still want per-set mapping but without `DataTable` allocation.

## Support Matrix (multi-result)
```
Approach                        | SQL Server                | PostgreSQL                         | Oracle
--------------------------------|---------------------------|------------------------------------|------------------------------
DataTables via QueryTablesAsync | ✅ multi-SELECT/procs      | ✅ refcursor outputs               | ✅ RefCursor outputs
Streaming multi-sets            | ✅ via QueryMultiAsync     | ✅ via QueryMultiAsync (streams refcursors) | ✅ via QueryMultiAsync (streams refcursors)
Refcursor requirement           | No                        | Yes (multi-result procs)           | Yes (multi-result procs)
```

DataReader note: `QueryMultiAsync` streams via `IDataReader` for direct multi-sets (e.g., SQL Server). For refcursor-based procs (PostgreSQL/Oracle), it now streams each cursor in order without `DataTable`/`DataSet` allocation.

## Full example (SQL Server)
Stored proc signature (example):
```sql
create procedure dbo.GetCustomerAndOrders
    @customerId int
as
begin
    set nocount on;
    select Id, Name from dbo.Customers where Id = @customerId;
    select Id, CustomerId, Amount from dbo.Orders where CustomerId = @customerId;
end
```

C# call with input/output mapping:
```csharp
var result = await executor.QueryTablesAsync(new CommandDefinition
{
    CommandText = "dbo.GetCustomerAndOrders",
    CommandType = CommandType.StoredProcedure,
    Parameters = new[]
    {
        new DbParameter("customerId", DbDataType.Int32, value: 42, direction: ParameterDirection.Input)
    }
});

if (!result.Success) throw new DbCallerException(result.Error!);

// Output tables (in order):
// Tables[0]: Customers -> columns: Id (int), Name (string)
// Tables[1]: Orders    -> columns: Id (int), CustomerId (int), Amount (decimal)

var customers = result.Tables![0]
    .AsEnumerable()
    .Select(r => new Customer { Id = r.Field<int>("Id"), Name = r.Field<string>("Name") })
    .ToList();

var orders = result.Tables![1]
    .AsEnumerable()
    .Select(r => new Order { Id = r.Field<int>("Id"), CustomerId = r.Field<int>("CustomerId"), Amount = r.Field<decimal>("Amount") })
    .ToList();
```

The same pattern applies to PostgreSQL and Oracle multi-result stored procedures that return refcursors: supply the input parameter(s) plus refcursor outputs, call `QueryTablesAsync`, then map `Tables[0]`, `Tables[1]`, etc. to your classes.

## Streaming example (SQL Server / direct multi-sets)
Use when the provider returns multiple result sets directly (e.g., SQL Server). `SetIndex` is the zero-based result-set index: 0 for the first SELECT, 1 for the second, and so on.
```csharp
await foreach (var row in executor.QueryMultiAsync(
    new CommandDefinition
    {
        CommandText = @"
            select Id, Name from dbo.Customers where Id = @id;
            select Id, CustomerId, Amount from dbo.Orders where CustomerId = @id;",
        CommandType = CommandType.Text,
        Parameters = new[] { new DbParameter("id", DbDataType.Int32, value: 42) }
    },
    new Func<IDataRecord, object>[]
    {
        r => new Customer { Id = r.Get<int>(0), Name = r.Get<string>(1) },
        r => new Order { Id = r.Get<int>(0), CustomerId = r.Get<int>(1), Amount = r.Get<decimal>(2) }
    }))
{
    if (row.SetIndex == 0)
    {
        var customer = (Customer)row.Item;
        // handle customer
    }
    else if (row.SetIndex == 1)
    {
        var order = (Order)row.Item;
        // handle order
    }
}
```
For PostgreSQL/Oracle refcursor-based multi-sets, `QueryMultiAsync` streams each cursor in order (no DataTable buffering).

`SetIndex` is the zero-based result-set index: 0 for the first result set, 1 for the second, etc.

## Streaming example (PostgreSQL refcursor proc)
Order of output refcursor parameters controls `SetIndex` (first cursor = 0, second = 1, etc.).
```csharp
var cmd = new CommandDefinition
{
    CommandText = "public.get_customer_and_orders",
    CommandType = CommandType.StoredProcedure,
    Parameters = new[]
    {
        new DbParameter("customer_id", DbDataType.Int32, value: 42, direction: ParameterDirection.Input),
        // refcursor outputs in the order you want to consume them
        new DbParameter("customer_cursor", DbDataType.RefCursor, direction: ParameterDirection.Output),
        new DbParameter("orders_cursor", DbDataType.RefCursor, direction: ParameterDirection.Output)
    }
};

var customers = new List<Customer>();
var orders = new List<Order>();

await foreach (var row in executor.QueryMultiAsync(
    cmd,
    new Func<IDataRecord, object>[]
    {
        r => new Customer { Id = r.Get<int>(0), Name = r.Get<string>(1) },
        r => new Order { Id = r.Get<int>(0), CustomerId = r.Get<int>(1), Amount = r.Get<decimal>(2) }
    }))
{
    if (row.SetIndex == 0) { customers.Add((Customer)row.Item); }
    else if (row.SetIndex == 1) { orders.Add((Order)row.Item); }
}

// customers -> all rows from customer_cursor; orders -> all rows from orders_cursor
```

## Streaming example (Oracle RefCursor proc)
Order of `RefCursor` output parameters controls `SetIndex`.
```csharp
var cmd = new CommandDefinition
{
    CommandText = "PKG_CUSTOMER.GET_CUSTOMER_AND_ORDERS",
    CommandType = CommandType.StoredProcedure,
    Parameters = new[]
    {
        new DbParameter("p_customer_id", DbDataType.Int32, value: 42, direction: ParameterDirection.Input),
        new DbParameter("p_customer", DbDataType.RefCursor, direction: ParameterDirection.Output),
        new DbParameter("p_orders", DbDataType.RefCursor, direction: ParameterDirection.Output)
    }
};

var customers = new List<Customer>();
var orders = new List<Order>();

await foreach (var row in executor.QueryMultiAsync(
    cmd,
    new Func<IDataRecord, object>[]
    {
        r => new Customer { Id = r.Get<int>(0), Name = r.Get<string>(1) },
        r => new Order { Id = r.Get<int>(0), CustomerId = r.Get<int>(1), Amount = r.Get<decimal>(2) }
    }))
{
    if (row.SetIndex == 0) { customers.Add((Customer)row.Item); }
    else if (row.SetIndex == 1) { orders.Add((Order)row.Item); }
}

// customers -> all rows from p_customer; orders -> all rows from p_orders
```

## Refcursor example (PostgreSQL)
```csharp
var result = await executor.QueryTablesAsync(new CommandDefinition
{
    CommandText = "public.get_customer_and_orders",
    CommandType = CommandType.StoredProcedure,
    Parameters = new[]
    {
        new DbParameter("customer_id", DbDataType.Int32, value: 42, direction: ParameterDirection.Input),
        new DbParameter("customer_cursor", DbDataType.RefCursor, direction: ParameterDirection.Output),
        new DbParameter("orders_cursor", DbDataType.RefCursor, direction: ParameterDirection.Output)
    }
});
if (!result.Success) throw new DbCallerException(result.Error!);
// result.Tables[0] = customers, result.Tables[1] = orders
```

## RefCursor example (Oracle)
```csharp
var result = await executor.QueryTablesAsync(new CommandDefinition
{
    CommandText = "PKG_CUSTOMER.GET_CUSTOMER_AND_ORDERS",
    CommandType = CommandType.StoredProcedure,
    Parameters = new[]
    {
        new DbParameter("p_customer_id", DbDataType.Int32, value: 42, direction: ParameterDirection.Input),
        new DbParameter("p_customer", DbDataType.RefCursor, direction: ParameterDirection.Output),
        new DbParameter("p_orders", DbDataType.RefCursor, direction: ParameterDirection.Output)
    }
});
if (!result.Success) throw new DbCallerException(result.Error!);
// result.Tables[0] = customers, result.Tables[1] = orders
```

## Testing notes (PostgreSQL/Oracle refcursor procs)
- Use `QueryTablesAsync` in tests and assert `result.Success == true`.
- Verify table counts and expected columns/rows per cursor:
```csharp
var result = await executor.QueryTablesAsync(cmd);
result.Success.Should().BeTrue();
result.Tables.Should().NotBeNull();
result.Tables!.Count.Should().Be(2);
result.Tables[0].Rows.Count.Should().BeGreaterThan(0);
```
This works the same for PostgreSQL refcursor procs and Oracle `RefCursor` procs.
