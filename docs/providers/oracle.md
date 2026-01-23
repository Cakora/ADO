# Oracle Provider Documentation

Applies when `DbOptions.DatabaseType = DatabaseType.Oracle`.

This provider uses:

- `Oracle.ManagedDataAccess.Client.OracleConnection`
- `Oracle.ManagedDataAccess.Client.OracleParameter`
- `Oracle.ManagedDataAccess.Client.OracleBulkCopy` (bulk import; synchronous API)
- `Oracle.ManagedDataAccess.Client.OracleDataAdapter` (buffering via `DataAdapter.Fill`)

---

## Supported Feature Combinations

| Feature | Supported | Notes |
|---|---:|---|
| Streaming (`ExecuteReaderAsync`, `StreamAsync`) | No | Oracle streaming is blocked to avoid partial refcursor result consumption |
| Streaming + output params (`ExecuteReaderWithOutputsAsync`) | No | not supported |
| Buffered single result (`QueryTableAsync`) | Yes | preferred Oracle read path |
| Buffered multi-result (`QueryTablesAsync`) | Yes | primary multi-result mechanism |
| Buffered `DataSet` (`ExecuteDataSetAsync`) | Yes | for refcursor procedures it routes through refcursor path |
| Output parameters (buffered) | Yes | non-refcursor outputs are extracted |
| Bulk import (`BulkImportAsync(BulkImportRequest)`) | Yes | uses `OracleBulkCopy` (sync) |
| Typed bulk import (`BulkImportAsync<T>` with linq2db) | Yes | requires `DbOptions.LinqToDb.Enable = true` |
| RefCursor multi-results | Yes | Oracle returns result sets via `RefCursor` output parameters |

---

## Parameter Naming Rules

Oracle commonly uses `:` parameters, but `OracleParameter.ParameterName` accepts plain names as well. AdoAsync does not rewrite names; pass the name you want.

Output extraction trims `@`, `:`, `?` prefixes when building the outputs dictionary:

- Declare `:p_status` → outputs dictionary key is `p_status`

---

## Oracle Reads (Buffered)

### Buffered mapping (simple)

```csharp
using System.Data;
using AdoAsync;
using AdoAsync.Execution;

await using var executor = DbExecutor.Create(options);

var table = await executor.QueryTableAsync(new CommandDefinition
{
    CommandText = "select Id, Name from Customers where Id >= :min_id",
    CommandType = CommandType.Text,
    Parameters = new[]
    {
        new DbParameter { Name = ":min_id", DataType = DbDataType.Int32, Direction = ParameterDirection.Input, Value = 100 }
    }
});

foreach (DataRow row in table.Rows)
{
    var id = row.Field<int>("Id");
    var name = row.Field<string>("Name");
}
```

Notes:

- Oracle uses buffered calls to avoid issues with cursor-driven result sets.

---

## RefCursor Multi-Result Sets (Oracle)

Oracle stored procedures return result sets via output parameters of type `RefCursor`.

In AdoAsync:

- Declare cursor parameters with `DbDataType.RefCursor` and `Direction = Output`
- Use `QueryTablesAsync` (multi-result) or `QueryTableAsync` (first cursor only)

```csharp
using System.Data;

var tables = await executor.QueryTablesAsync(new CommandDefinition
{
    CommandText = "PKG_CUSTOMER.GET_CUSTOMER_AND_ORDERS",
    CommandType = CommandType.StoredProcedure,
    Parameters = new[]
    {
        new DbParameter { Name = "p_customer_id", DataType = DbDataType.Int32, Direction = ParameterDirection.Input, Value = 42 },
        new DbParameter { Name = "p_customer", DataType = DbDataType.RefCursor, Direction = ParameterDirection.Output },
        new DbParameter { Name = "p_orders", DataType = DbDataType.RefCursor, Direction = ParameterDirection.Output }
    }
});

var customerTable = tables[0];
var ordersTable = tables[1];
```

### RefCursor + output parameters (combined)

If the procedure also returns non-cursor outputs, they are extracted and attached to the first table:

```csharp
using AdoAsync.Extensions.Execution;

var outputs = tables[0].GetOutputParameters();
```

---

## Output Parameters (Oracle; buffered)

For output parameters without refcursors, use `QueryTableAsync` (it can return an empty table and still surface outputs):

```csharp
using System.Data;
using AdoAsync.Extensions.Execution;

var table = await executor.QueryTableAsync(new CommandDefinition
{
    CommandText = "PKG_ITEMS.UPDATE_AND_RETURN_STATUS",
    CommandType = CommandType.StoredProcedure,
    Parameters = new[]
    {
        new DbParameter { Name = "p_id", DataType = DbDataType.Int32, Direction = ParameterDirection.Input, Value = 42 },
        new DbParameter { Name = "p_status", DataType = DbDataType.Int32, Direction = ParameterDirection.Output },
        new DbParameter { Name = "p_message", DataType = DbDataType.String, Direction = ParameterDirection.Output, Size = 4000 }
    }
});

var outputs = table.GetOutputParameters();
var status = (int?)outputs?["p_status"];
var message = (string?)outputs?["p_message"];
```

---

## Bulk Import (Oracle)

Oracle bulk import uses `OracleBulkCopy` (synchronous API). Cancellation is checked before the call.

```csharp
using var sourceReader = GetSourceReader(); // DbDataReader with columns: ID, NAME

var request = new BulkImportRequest
{
    DestinationTable = "ITEMS",
    SourceReader = sourceReader,
    ColumnMappings = new[]
    {
        new BulkImportColumnMapping { SourceColumn = "ID", DestinationColumn = "ID" },
        new BulkImportColumnMapping { SourceColumn = "NAME", DestinationColumn = "NAME" }
    },
    AllowedDestinationTables = new HashSet<string> { "ITEMS" },
    AllowedDestinationColumns = new HashSet<string> { "ID", "NAME" }
};

var result = await executor.BulkImportAsync(request);
```

Notes:

- `DbExecutor` normalizes Oracle destination table identifiers for bulk import.

---

## Recommended Options

- Keep `EnableValidation = true` for identifier allow-lists (bulk import + dynamic identifiers).
- Prefer buffered patterns (`QueryTableAsync` / `QueryTablesAsync`) for reads and stored procedures.

