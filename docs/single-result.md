# Single Result Patterns (All Providers)

Use these patterns for single result sets across SQL Server, PostgreSQL, and Oracle.

## Streaming (fast, low memory) via `QueryAsync<T>`
Works on all providers; map rows to your type.
```csharp
// Streaming pattern (IAsyncEnumerable)
await foreach (var customer in executor.QueryAsync(
    new CommandDefinition
    {
        CommandText = "select Id, Name from Customers where IsActive = @active",
        CommandType = CommandType.Text,
        Parameters = new[]
        {
            new DbParameter("active", DbDataType.Boolean, value: true)
        }
    },
    record => new Customer
    {
        Id = record.Get<int>(0),
        Name = record.Get<string>(1)
    }))
{
    // process customer
}

// Buffered list (no streaming): materialize the stream
var customers = await executor.QueryAsync(
    new CommandDefinition
    {
        CommandText = "select Id, Name from Customers where IsActive = @active",
        CommandType = CommandType.Text,
        Parameters = new[]
        {
            new DbParameter("active", DbDataType.Boolean, value: true)
        }
    },
    record => new Customer
    {
        Id = record.Get<int>(0),
        Name = record.Get<string>(1)
    })
    .ToListAsync();
```

## Buffered `DataTable` via `QueryTablesAsync`
Returns the single result set as `DataTable` (cross-provider).
```csharp
var result = await executor.QueryTablesAsync(new CommandDefinition
{
    CommandText = "select Id, Name from Customers where IsActive = @active",
    CommandType = CommandType.Text,
    Parameters = new[]
    {
        new DbParameter("active", DbDataType.Boolean, value: true)
    }
});

if (!result.Success) throw new DbCallerException(result.Error!);

var table = result.Tables![0];
foreach (DataRow row in table.Rows)
{
    var id = row.Field<int>("Id");
    var name = row.Field<string>("Name");
}
```

## Stored procedure (single result) without refcursor
Use plain input parameters; works on all providers.
```csharp
var result = await executor.QueryTablesAsync(new CommandDefinition
{
    CommandText = "dbo.GetActiveCustomers", // or public.get_active_customers / PKG.GET_CUSTOMERS
    CommandType = CommandType.StoredProcedure,
    Parameters = new[]
    {
        new DbParameter("is_active", DbDataType.Boolean, value: true, direction: ParameterDirection.Input)
    }
});

if (!result.Success) throw new DbCallerException(result.Error!);
var table = result.Tables![0];
```

## Stored procedure returning a single refcursor (Oracle only)
Only needed if the proc returns a refcursor; otherwise use the patterns above.
```csharp
var result = await executor.QueryTablesAsync(new CommandDefinition
{
    CommandText = "PKG_CUSTOMER.GET_ACTIVE",
    CommandType = CommandType.StoredProcedure,
    Parameters = new[]
    {
        new DbParameter("p_is_active", DbDataType.Boolean, value: true, direction: ParameterDirection.Input),
        new DbParameter("p_cursor", DbDataType.RefCursor, direction: ParameterDirection.Output)
    }
});

if (!result.Success) throw new DbCallerException(result.Error!);
var table = result.Tables![0];
```

## Support Matrix
```
Approach                     | SQL Server | PostgreSQL | Oracle
-----------------------------|-----------|------------|--------
DataTable (QueryTablesAsync) | ✅         | ✅          | ✅
Streaming (QueryAsync<T>)    | ✅         | ✅          | ✅
Single refcursor             | n/a       | not needed  | ✅ if proc returns refcursor

DataReader (QueryAsync<T>): uses `IDataReader` internally; no refcursor required for single result sets on any provider. Only add refcursors when your stored procedure explicitly returns them (Oracle) or for multi-result scenarios.
```

## Which to use?
- Fastest / lowest memory: `QueryAsync<T>` (streams via `IDataReader`).
- Buffered (no streaming): `QueryTablesAsync` to get a `DataTable`, then map to your own list if needed.
- Refcursor: only for Oracle stored procs that return a refcursor (and PostgreSQL for multi-result procs). Not needed for a single SELECT/stored proc returning one result set.

DataReader note: `QueryAsync<T>` uses `IDataReader` internally and does not require refcursors for single result sets on any provider (SQL Server, PostgreSQL, Oracle). Use refcursors only when your stored procedure explicitly returns them (Oracle) or for multi-result scenarios.
