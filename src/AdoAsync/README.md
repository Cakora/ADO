# AdoAsync

Async-first ADO.NET helper that keeps provider logic contained for SQL Server, PostgreSQL, and Oracle. It enforces validation, explicit transactions, optional Polly retries, and returns a custom error contract instead of leaking provider exceptions.

## Features
- Async-only API surface for commands, scalars, streaming queries, and multi-result sets.
- FluentValidation-backed options/command/parameter checks (opt-in via `EnableValidation`).
- Retry policies (Polly) that are off by default and skip retries inside user transactions.
- Provider-specific type and exception mappers kept in dedicated namespaces.
- Explicit transaction handles (`IAsyncDisposable`) with rollback-on-dispose semantics.
- Diagnostics off by default; returned via result contracts instead of logging.
- Explicit bulk import with column mapping (provider-owned implementations).

## Quick Start
```csharp
var options = new DbOptions
{
    DatabaseType = DatabaseType.SqlServer,
    ConnectionString = "...",
    CommandTimeoutSeconds = 30,
    EnableValidation = true,
    EnableRetry = false
};

await using var executor = DbExecutor.Create(options);

var rows = await executor.ExecuteAsync(new CommandDefinition(
    "update dbo.Items set Processed = 1 where Id = @id",
    new[] { new DbParameter("id", DbDataType.Int32, 42) }));
```

See `IMPLEMENTATION_GUIDE.md` for the full behavior and guardrails.

You can also supply a provider `DbDataSource` via `DbOptions.DataSource` to reuse configured data sources when supported by your provider.

## Bulk Import (Optional)
```csharp
using var reader = GetSourceReader();
var request = new BulkImportRequest
{
    DestinationTable = "dbo.Items",
    SourceReader = reader,
    ColumnMappings = new[]
    {
        new BulkImportColumnMapping { SourceColumn = "Id", DestinationColumn = "Id" },
        new BulkImportColumnMapping { SourceColumn = "Name", DestinationColumn = "Name" }
    },
    AllowedDestinationTables = new HashSet<string> { "dbo.Items" },
    AllowedDestinationColumns = new HashSet<string> { "Id", "Name" }
};

var result = await executor.BulkImportAsync(request);
```
