# PostgreSQL Provider Documentation

Applies when `DbOptions.DatabaseType = DatabaseType.PostgreSql`.

This provider uses:

- `Npgsql.NpgsqlConnection`
- `Npgsql.NpgsqlParameter`
- `Npgsql.NpgsqlDataAdapter` (buffering via `DataAdapter.Fill`)
- Binary `COPY` for bulk import (`COPY ... FROM STDIN (FORMAT BINARY)`)

---

## Supported Feature Combinations

| Feature | Supported | Notes |
|---|---:|---|
| Streaming (`ExecuteReaderAsync`, `StreamAsync`) | Yes | best performance; output params available after reader is closed (if declared) |
| Buffered single result (`QueryTableAsync`) | Yes | output params attached to `DataTable.ExtendedProperties` |
| Buffered multi-result (`QueryTablesAsync`) | Yes | supports multi-SELECT SQL and refcursor procedures |
| Buffered `DataSet` (`ExecuteDataSetAsync`) | Yes | for multi-SELECT; for refcursor it routes through refcursor path |
| Output parameters (buffered) | Yes | non-refcursor outputs are extracted; refcursors are treated as result sets |
| Bulk import (`BulkImportAsync(BulkImportRequest)`) | Yes | implemented via binary `COPY` |
| Typed bulk import (`BulkImportAsync<T>` with linq2db) | Yes | requires `DbOptions.LinqToDb.Enable = true` |
| RefCursor multi-results | Yes | uses output params of type `DbDataType.RefCursor`; consumed in a transaction |

---

## Parameter Naming Rules

PostgreSQL typically uses names without `@`:

- Input: `customer_id`
- Output: `total`, `customer_cursor`, `orders_cursor`

Output extraction trims prefixes (`@`, `:`, `?`) if you include them, but preferred PostgreSQL style is no prefix.

---

## Streaming (PostgreSQL)

Streaming is supported for single SELECT statements and procedures that return rowsets directly.

```csharp
using System.Data;
using AdoAsync.Common;
using AdoAsync.Execution;

await foreach (var customer in executor.QueryAsync(
    new CommandDefinition
    {
        CommandText = "select id, name from customers where id >= @min_id",
        CommandType = CommandType.Text,
        Parameters = new[]
        {
            new DbParameter { Name = "min_id", DataType = DbDataType.Int32, Direction = ParameterDirection.Input, Value = 100 }
        }
    },
    record => new Customer
    {
        Id = record.Get<int>("id") ?? 0,
        Name = record.Get<string>("name")
    }))
{
    // process customer
}
```

Notes:

- Streaming is **not** the right mechanism for PostgreSQL `refcursor` outputs. Use buffered refcursor patterns below.

---

## RefCursor Multi-Result Sets (PostgreSQL)

PostgreSQL stored procedures can return multiple refcursors via output parameters. In AdoAsync:

- Declare output cursor parameters as `DbDataType.RefCursor` with `Direction = Output`.
- Use `QueryTablesAsync` (or `QueryTableAsync` if you only need the first cursor).
- The executor ensures refcursors are opened and fetched **within a transaction**, which PostgreSQL requires.

```csharp
using System.Data;

var tables = await executor.QueryTablesAsync(new CommandDefinition
{
    CommandText = "public.get_customer_and_orders",
    CommandType = CommandType.StoredProcedure,
    Parameters = new[]
    {
        new DbParameter { Name = "customer_id", DataType = DbDataType.Int32, Direction = ParameterDirection.Input, Value = 42 },
        new DbParameter { Name = "customer_cursor", DataType = DbDataType.RefCursor, Direction = ParameterDirection.Output },
        new DbParameter { Name = "orders_cursor", DataType = DbDataType.RefCursor, Direction = ParameterDirection.Output }
    }
});

var customerTable = tables[0];
var ordersTable = tables[1];
```

### RefCursor + output parameters (combined)

`QueryTablesAsync` only returns tables. If you need output parameters too, call `ExecuteDataSetAsync` and use the returned tuple’s `OutputParameters`.

---

## Buffered Output Parameters (PostgreSQL)

For output parameters without refcursors, use a buffered call:

```csharp
using System.Data;

(DataTable Table, IReadOnlyDictionary<string, object?> OutputParameters) result =
    await executor.QueryTableAsync(new CommandDefinition
{
    CommandText = "public.update_and_return_status",
    CommandType = CommandType.StoredProcedure,
    Parameters = new[]
    {
        new DbParameter { Name = "id", DataType = DbDataType.Int32, Direction = ParameterDirection.Input, Value = 42 },
        new DbParameter { Name = "status", DataType = DbDataType.Int32, Direction = ParameterDirection.Output },
        new DbParameter { Name = "message", DataType = DbDataType.String, Direction = ParameterDirection.Output, Size = 4000 }
    }
});

var status = (int?)result.OutputParameters["status"];
var message = (string?)result.OutputParameters["message"];
```

---

## Bulk Import (PostgreSQL)

PostgreSQL bulk import is implemented via **binary COPY** for high throughput.

```csharp
using var sourceReader = GetSourceReader(); // DbDataReader with columns: Id, Name

var request = new BulkImportRequest
{
    DestinationTable = "public.items",
    SourceReader = sourceReader,
    ColumnMappings = new[]
    {
        new BulkImportColumnMapping { SourceColumn = "Id", DestinationColumn = "id" },
        new BulkImportColumnMapping { SourceColumn = "Name", DestinationColumn = "name" }
    },
    AllowedDestinationTables = new HashSet<string> { "public.items" },
    AllowedDestinationColumns = new HashSet<string> { "id", "name" }
};

var result = await executor.BulkImportAsync(request);
```

Notes:

- The destination identifier is quoted internally for COPY generation.
- The request uses allow-lists for destination table/columns when validation is enabled.

---

## Recommended Options

- Keep `EnableValidation = true` to protect COPY identifier usage (tables/columns).
- For refcursor procedures, avoid manual transaction management unless you need to group multiple operations; the executor creates a transaction scope when required.
