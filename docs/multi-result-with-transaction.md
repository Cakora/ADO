# Multi-Result With Transaction (Cross-Provider)

Example: SQL Server stored proc with two result sets, executed in one explicit transaction.
```csharp
// Proc (example)
// create procedure dbo.GetCustomerAndOrders @customerId int as
// begin set nocount on;
//   select Id, Name from dbo.Customers where Id = @customerId;
//   select Id, CustomerId, Amount from dbo.Orders where CustomerId = @customerId;
// end

await using var exec = DbExecutor.Create(options);
await using var tx = await exec.BeginTransactionAsync();

await foreach (var row in exec.QueryMultiAsync(
    new CommandDefinition
    {
        CommandText = "dbo.GetCustomerAndOrders",
        CommandType = CommandType.StoredProcedure,
        Parameters = new[]
        {
            new DbParameter("customerId", DbDataType.Int32, value: 42, direction: ParameterDirection.Input)
        }
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

await tx.CommitAsync();
```
Notes:
- `BeginTransactionAsync` wraps the entire multi-result execution.
- `SetIndex` is zero-based per result set.
- For PostgreSQL/Oracle refcursor procs, `QueryMultiAsync` streams cursors in parameter order; the same transaction pattern applies.
