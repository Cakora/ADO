# Multi-Result Without Transaction (Cross-Provider)

Example: SQL Server multi-SELECT batch without an explicit transaction.
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
    if (row.SetIndex == 0) { /* handle Customer */ }
    else if (row.SetIndex == 1) { /* handle Order */ }
}
```

PostgreSQL/Oracle refcursor procs:
```csharp
await foreach (var row in executor.QueryMultiAsync(
    new CommandDefinition
    {
        CommandText = "public.get_customer_and_orders",
        CommandType = CommandType.StoredProcedure,
        Parameters = new[]
        {
            new DbParameter("customer_id", DbDataType.Int32, value: 42, direction: ParameterDirection.Input),
            new DbParameter("customer_cursor", DbDataType.RefCursor, direction: ParameterDirection.Output),
            new DbParameter("orders_cursor", DbDataType.RefCursor, direction: ParameterDirection.Output)
        }
    },
    new Func<IDataRecord, object>[]
    {
        r => new Customer { Id = r.Get<int>(0), Name = r.Get<string>(1) },
        r => new Order { Id = r.Get<int>(0), CustomerId = r.Get<int>(1), Amount = r.Get<decimal>(2) }
    }))
{
    if (row.SetIndex == 0) { /* customer rows */ }
    else if (row.SetIndex == 1) { /* order rows */ }
}
```
Notes:
- No explicit transaction; each command runs with default connection semantics.
- `SetIndex` follows output parameter order (refcursor) or SELECT order (multi-SELECT).
