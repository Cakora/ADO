# SQL Server Examples

These examples use the same API surface across providers.

## Single SELECT -> DataTable
```csharp
var result = await executor.QueryTablesAsync(new CommandDefinition
{
    CommandText = "select Id, Name from dbo.Customers",
    CommandType = CommandType.Text
});

var table = result.Tables![0];
```

## Stored Procedure -> DataTable (input params)
```csharp
var result = await executor.QueryTablesAsync(new CommandDefinition
{
    CommandText = "dbo.GetCustomer",
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
```

## Stored Procedure -> Multiple Result Sets -> DataSet
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

var dataSet = new DataSet();
foreach (var table in result.Tables!)
{
    dataSet.Tables.Add(table);
}
```

## SELECT -> Custom Class (explicit mapper)
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

## Stored Procedure with Output Parameters
```csharp
var result = await executor.QueryTablesAsync(new CommandDefinition
{
    CommandText = "dbo.GetCustomerStats",
    CommandType = CommandType.StoredProcedure,
    Parameters = new[]
    {
        new DbParameter
        {
            Name = "@customerId",
            DataType = DbDataType.Int32,
            Direction = ParameterDirection.Input,
            Value = 42
        },
        new DbParameter
        {
            Name = "@orderCount",
            DataType = DbDataType.Int32,
            Direction = ParameterDirection.Output,
            Size = 4
        }
    }
});

var outputs = result.OutputParameters;
var orderCount = outputs?["orderCount"];
```
