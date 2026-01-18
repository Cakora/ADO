# PostgreSQL Examples

These examples use the same API surface across providers.

## Single SELECT -> DataTable
```csharp
var result = await executor.QueryTablesAsync(new CommandDefinition
{
    CommandText = "select id, name from public.customers",
    CommandType = CommandType.Text
});

var table = result.Tables![0];
```

## Stored Procedure -> DataTable (input params)
```csharp
var result = await executor.QueryTablesAsync(new CommandDefinition
{
    CommandText = "public.get_customer",
    CommandType = CommandType.StoredProcedure,
    Parameters = new[]
    {
        new DbParameter
        {
            Name = "@customer_id",
            DataType = DbDataType.Int32,
            Direction = ParameterDirection.Input,
            Value = 42
        }
    }
});
```

## Stored Procedure -> Multiple Result Sets -> DataSet (refcursor outputs)
```csharp
var result = await executor.QueryTablesAsync(new CommandDefinition
{
    CommandText = "public.get_customer_and_orders",
    CommandType = CommandType.StoredProcedure,
    Parameters = new[]
    {
        new DbParameter
        {
            Name = "@p_customer_id",
            DataType = DbDataType.Int32,
            Direction = ParameterDirection.Input,
            Value = 42
        },
        new DbParameter
        {
            Name = "@p_customer_cursor",
            DataType = DbDataType.RefCursor,
            Direction = ParameterDirection.Output
        },
        new DbParameter
        {
            Name = "@p_orders_cursor",
            DataType = DbDataType.RefCursor,
            Direction = ParameterDirection.Output
        }
    }
});

var dataSet = new DataSet();
foreach (var table in result.Tables!)
{
    dataSet.Tables.Add(table);
}
```

## SELECT -> Custom Class (explicit mapper)
```csharp
await foreach (var customer in executor.QueryAsync(new CommandDefinition
{
    CommandText = "select id, name from public.customers",
    CommandType = CommandType.Text
}, record => new Customer
{
    Id = record.GetInt32(0),
    Name = record.GetString(1)
}))
{
    // use customer
}
```

## Stored Procedure with Output Parameters
```csharp
var result = await executor.QueryTablesAsync(new CommandDefinition
{
    CommandText = "public.get_customer_stats",
    CommandType = CommandType.StoredProcedure,
    Parameters = new[]
    {
        new DbParameter
        {
            Name = "@p_customer_id",
            DataType = DbDataType.Int32,
            Direction = ParameterDirection.Input,
            Value = 42
        },
        new DbParameter
        {
            Name = "@p_order_count",
            DataType = DbDataType.Int32,
            Direction = ParameterDirection.Output,
            Size = 4
        }
    }
});

var outputs = result.OutputParameters;
var orderCount = outputs?["p_order_count"];
```
