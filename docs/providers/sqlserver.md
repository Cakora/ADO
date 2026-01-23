# SQL Server Provider Documentation

Applies when `DbOptions.DatabaseType = DatabaseType.SqlServer`.

This provider uses:

- `Microsoft.Data.SqlClient.SqlConnection`
- `Microsoft.Data.SqlClient.SqlParameter`
- `Microsoft.Data.SqlClient.SqlBulkCopy`
- `Microsoft.Data.SqlClient.SqlDataAdapter` (buffering via `DataAdapter.Fill`)

---

## Supported Feature Combinations

| Feature | Supported | Notes |
|---|---:|---|
| Streaming (`ExecuteReaderAsync`, `StreamAsync`) | Yes | Best performance; output params not returned |
| Streaming + output params (`ExecuteReaderWithOutputsAsync`) | Yes | outputs available after reader is closed |
| Buffered single result (`QueryTableAsync`) | Yes | output params returned in tuple |
| Buffered multi-result (`QueryTablesAsync`) | Yes | multiple `SELECT` or stored procedure results |
| Buffered `DataSet` (`ExecuteDataSetAsync`) | Yes | multi-result as DataSet |
| Output parameters (buffered) | Yes | returned in tuple |
| Bulk import (`BulkImportAsync(BulkImportRequest)`) | Yes | implemented via `SqlBulkCopy` |
| Typed bulk import (`BulkImportAsync<T>` with linq2db) | Yes | requires `DbOptions.LinqToDb.Enable = true` |
| RefCursor multi-results | No | SQL Server doesn’t use refcursors |

---

## Parameter Naming Rules

SQL Server commonly uses `@` parameters:

- Input: `@id`, `@name`
- Output: `@out_status`, `@out_message`

Output extraction trims prefixes (`@`, `:`, `?`) when building the output dictionary:

- Declare `@total` → outputs dictionary key is `total`

---

## Fastest Usage Patterns (SQL Server)

### 1) Fastest read (manual `DbDataReader`)

```csharp
using System.Data;
using AdoAsync;
using AdoAsync.Execution;

await using var executor = DbExecutor.Create(options);
await using var reader = await executor.ExecuteReaderAsync(new CommandDefinition
{
    CommandText = "select Id, Name from dbo.Customers where Id >= @minId",
    CommandType = CommandType.Text,
    Parameters = new[]
    {
        new DbParameter { Name = "@minId", DataType = DbDataType.Int32, Direction = ParameterDirection.Input, Value = 100 }
    }
});

while (await reader.ReadAsync())
{
    var id = reader.GetInt32(0);
    var name = reader.GetString(1);
}
```

### 2) Streaming mapping (recommended)

```csharp
using System.Data;
using AdoAsync.Common;
using AdoAsync.Execution;

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
    // process customer
}
```

---

## Output Parameters (SQL Server)

### Buffered outputs (recommended)

Use `QueryTableAsync` if your stored procedure returns no rowset but you need output params:

```csharp
using System.Collections.Generic;
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

### Streaming outputs (only if you must stream rows)

```csharp
await using var result = await executor.ExecuteReaderWithOutputsAsync(new CommandDefinition
{
    CommandText = "dbo.GetCustomersAndReturnTotal",
    CommandType = CommandType.StoredProcedure,
    Parameters = new[]
    {
        new DbParameter { Name = "@minId", DataType = DbDataType.Int32, Direction = ParameterDirection.Input, Value = 100 },
        new DbParameter { Name = "@total", DataType = DbDataType.Int32, Direction = ParameterDirection.Output }
    }
});

await using (result.Reader)
{
    while (await result.Reader.ReadAsync())
    {
        // stream rows
    }
}

var outputs = await result.GetOutputParametersAsync();
var total = (int?)outputs?["total"];
```

---

## Multi-Result Sets (SQL Server)

SQL Server supports multiple result sets from:

- Multiple `SELECT` statements in one batch
- Stored procedures that emit multiple results

```csharp
var tables = await executor.QueryTablesAsync(new CommandDefinition
{
    CommandText = "dbo.GetCustomerAndOrders",
    CommandType = CommandType.StoredProcedure,
    Parameters = new[]
    {
        new DbParameter { Name = "@customerId", DataType = DbDataType.Int32, Direction = ParameterDirection.Input, Value = 42 }
    }
});

var customerTable = tables[0];
var ordersTable = tables[1];
```

---

## Bulk Import (SQL Server)

SQL Server bulk import uses `SqlBulkCopy` with streaming enabled.

```csharp
using var sourceReader = GetSourceReader(); // DbDataReader with columns: Id, Name

var request = new BulkImportRequest
{
    DestinationTable = "dbo.Items",
    SourceReader = sourceReader,
    ColumnMappings = new[]
    {
        new BulkImportColumnMapping { SourceColumn = "Id", DestinationColumn = "Id" },
        new BulkImportColumnMapping { SourceColumn = "Name", DestinationColumn = "Name" }
    },
    AllowedDestinationTables = new HashSet<string> { "dbo.Items" },
    AllowedDestinationColumns = new HashSet<string> { "Id", "Name" },
    BatchSize = 50_000
};

var result = await executor.BulkImportAsync(request);
```

---

## Recommended Options

- Keep `EnableValidation = true` (especially for bulk import allow-lists and identifier validation).
- Enable retries (`EnableRetry = true`) only when your workload can tolerate at-least-once semantics.
- Treat `DbExecutor` as Scoped; use `IDbExecutorFactory` for multi-DB apps.
