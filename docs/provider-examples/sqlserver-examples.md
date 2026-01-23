# SQL Server Examples

See full provider documentation: `docs/providers/sqlserver.md`.

---

## 1) Streaming query (fast)

```csharp
using System.Data;
using AdoAsync;
using AdoAsync.Common;
using AdoAsync.Execution;

await using var executor = DbExecutor.Create(new DbOptions
{
    DatabaseType = DatabaseType.SqlServer,
    ConnectionString = "...",
    CommandTimeoutSeconds = 30
});

await foreach (var customer in executor.QueryAsync(
    new CommandDefinition
    {
        CommandText = "select Id, Name from dbo.Customers where Id >= @minId",
        Parameters = new[]
        {
            new DbParameter { Name = "@minId", DataType = DbDataType.Int32, Direction = ParameterDirection.Input, Value = 100 }
        }
    },
    record => new Customer
    {
        Id = record.Get<int>(0) ?? 0,
        Name = record.Get<string>(1)
    }))
{
    // process
}
```

---

## 2) Stored procedure with output parameters (buffered)

```csharp
using System.Data;

(DataTable Table, IReadOnlyDictionary<string, object?> OutputParameters) result =
    await executor.QueryTableAsync(new CommandDefinition
{
    CommandText = "dbo.UpdateAndReturnStatus",
    CommandType = CommandType.StoredProcedure,
    Parameters = new[]
    {
        new DbParameter { Name = "@id", DataType = DbDataType.Int32, Direction = ParameterDirection.Input, Value = 42 },
        new DbParameter { Name = "@status", DataType = DbDataType.Int32, Direction = ParameterDirection.Output },
        new DbParameter { Name = "@message", DataType = DbDataType.String, Direction = ParameterDirection.Output, Size = 4000 }
    }
});

var status = (int?)result.OutputParameters["status"];
var message = (string?)result.OutputParameters["message"];
```
