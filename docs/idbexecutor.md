# IDbExecutor / DbExecutor Documentation

This repository provides an async-first ADO.NET execution facade with **three** supported providers:

- SQL Server (`DatabaseType.SqlServer`)
- PostgreSQL (`DatabaseType.PostgreSql`)
- Oracle (`DatabaseType.Oracle`)

The main caller-facing surface area is `AdoAsync.Abstractions.IDbExecutor`, implemented by `AdoAsync.Execution.DbExecutor`.

---

## 1) Code Structure (Repository Map)

### `src/AdoAsync` (Main library)

- `Abstractions/`
  - `IDbExecutor`, `IDbExecutorFactory`, `IDbProvider`
- `Core/`
  - `DbOptions`, `CommandDefinition`, `DbParameter`, `DbDataType`, `MultiResult`, etc.
- `Execution/`
  - `Execution/Async/DbExecutor.cs` (main implementation)
  - `Execution/Async/DbExecutor.RefCursor.cs` (refcursor routing and handling)
- `Providers/`
  - `SqlServer/` (`SqlConnection`, `SqlParameter`, `SqlBulkCopy`)
  - `PostgreSql/` (`NpgsqlConnection`, `NpgsqlParameter`, `COPY ... BINARY`)
  - `Oracle/` (`OracleConnection`, `OracleParameter`, `OracleBulkCopy`, `RefCursor`)
- `Transactions/`
  - `TransactionHandle` + transaction manager (rollback-on-dispose semantics)
- `Resilience/`
  - Polly retry policy (opt-in via `DbOptions.EnableRetry`)
- `Validation/`
  - FluentValidation-based input validation (opt-in via `DbOptions.EnableValidation`)
- `Extensions/`
  - DI and result helpers (some public, some internal)
- `BulkImport/`
  - `BulkImportRequest`, `BulkImportResult`, column mappings
- `BulkCopy/`
  - Optional linq2db typed bulk import path

### `src/AdoAsync.Common` (Shared helpers)

- Convenience helpers for mapping and conversions:
  - `AdoAsync.Common.DataRecordExtensions.Get<T>(...)`
  - CSV helpers, file helpers, etc.

### `examples/`

- `AdoAsync.Demo` is a minimal console app showing how to construct `DbExecutor` and run a query.

---

## 2) Core Types

### `DbOptions` (configuration)

`DbOptions` is the one place where you select provider + connection behavior:

- `DatabaseType` (SqlServer / PostgreSql / Oracle)
- `ConnectionString` **or** `DataSource`
- `CommandTimeoutSeconds` default timeout for commands
- `EnableValidation` (default `true`)
- `EnableRetry` (default `false`)
- `WrapProviderExceptions` (default `true`)
- `LinqToDb` (typed bulk import config; default disabled)

### `CommandDefinition` (what to execute)

Required:

- `CommandText` (SQL text or stored procedure name)

Optional:

- `CommandType` (`Text` or `StoredProcedure`)
- `Parameters` (`IReadOnlyList<DbParameter>`)
- `CommandTimeoutSeconds` (override options per command)
- `Behavior` (reader command behavior; default `CommandBehavior.Default`)
- `AllowedIdentifiers` + `IdentifiersToValidate` (dynamic identifier allow-list validation)

### `DbParameter` (how you pass inputs/outputs)

Required:

- `Name`
- `DataType` (`DbDataType`)
- `Direction` (`Input`, `Output`, `InputOutput`, `ReturnValue`)

Optional:

- `Value`
- `Size` (important for output strings/binary)
- `Precision`/`Scale` (decimals)

### `DbDataType` (cross-provider parameter type)

`DbDataType` is the provider-neutral type used to create provider parameters (`SqlDbType`, `NpgsqlDbType`, `OracleDbType`).

It includes common primitives plus `RefCursor` for PostgreSQL/Oracle cursor outputs.

---

## 3) IDbExecutor API (What You Can Call)

`IDbExecutor` is **async-only** and **not thread-safe** (treat it as Scoped per request).

### Output Parameters Support (No Confusion Matrix)

Output parameters are not “returned” the same way on every method. Use this table as the rule of truth:

| Method | Return | Output Params Supported? | How To Read Outputs |
|---|---|---:|---|
| `ExecuteReaderAsync` | `DbDataReader` | No | — |
| `ExecuteReaderWithOutputsAsync` | `StreamingReaderResult` | Yes (SQL Server/PostgreSQL) | `await result.GetOutputParametersAsync()` (after reader closed) |
| `StreamAsync` | `IAsyncEnumerable<IDataRecord>` | No | — |
| `ExecuteScalarAsync<T>` | `(T Value, IReadOnlyDictionary<string, object?> OutputParameters)` | Yes (all providers) | returned in tuple |
| `ExecuteAsync` | `(int RowsAffected, IReadOnlyDictionary<string, object?> OutputParameters)` | Yes (all providers) | returned in tuple |
| `QueryTableAsync` | `(DataTable Table, IReadOnlyDictionary<string, object?> OutputParameters)` | Yes (all providers) | returned in tuple |
| `QueryAsync<T>(DataRow map)` | `(List<T> Rows, IReadOnlyDictionary<string, object?> OutputParameters)` | Yes (all providers) | returned in tuple |
| `ExecuteDataSetAsync` | `(DataSet DataSet, IReadOnlyDictionary<string, object?> OutputParameters)` | Yes (all providers) | returned in tuple |

Notes:

- Streaming outputs exist only on `ExecuteReaderWithOutputsAsync` because ADO.NET output params are available after the reader is finished/closed.
- Tuple-returning output dictionaries use normalized keys (prefix trimmed): `@NewId` / `:NewId` / `?NewId` → `"NewId"`.

### Streaming APIs (fastest, lowest memory)

- `ExecuteReaderAsync(...)` → `DbDataReader`
  - Provider support: **SQL Server, PostgreSQL**
  - Output parameters: **not returned**
  - You own reader lifecycle (dispose it).

- `ExecuteReaderWithOutputsAsync(...)` → `StreamingReaderResult`
  - Provider support: **SQL Server, PostgreSQL**
  - Output parameters: available only **after** the reader is closed:
    - `await result.GetOutputParametersAsync()`

- `StreamAsync(...)` → `IAsyncEnumerable<IDataRecord>`
  - Provider support: **SQL Server, PostgreSQL**
  - Output parameters: **not returned**

There is also a convenience wrapper:

- `AdoAsync.Execution.DbExecutorQueryExtensions.QueryAsync<T>(...)` → `IAsyncEnumerable<T>`
  - Provider support: **SQL Server, PostgreSQL**
  - Built on `StreamAsync`.

### Buffered APIs (materialize results; easiest)

- `ExecuteScalarAsync<T>(...)` → `(T Value, IReadOnlyDictionary<string, object?> OutputParameters)`
  - Provider support: **all**
  - Output parameters: **returned**

- `ExecuteAsync(...)` → `(int RowsAffected, IReadOnlyDictionary<string, object?> OutputParameters)`
  - Provider support: **all**
  - Output parameters: **returned**

- `QueryTableAsync(...)` → `(DataTable Table, IReadOnlyDictionary<string, object?> OutputParameters)`
  - Provider support: **all**
  - Output parameters: returned in tuple

- `QueryAsync<T>(..., Func<DataRow,T> map)` → `(List<T> Rows, IReadOnlyDictionary<string, object?> OutputParameters)`
  - Provider support: **all**
  - This buffers using `QueryTableAsync` then maps rows.

- `ExecuteDataSetAsync(...)` → `(DataSet DataSet, IReadOnlyDictionary<string, object?> OutputParameters)`
  - Provider support: **all**
  - Output parameters: returned in tuple

### DbExecutor-only APIs (not on the interface)

`DbExecutor` (concrete) adds:

- `QueryTablesAsync(...)` → `IReadOnlyList<DataTable>` (multi-result convenience)
- `BulkImportAsync(...)` (provider fast bulk import)
- `BulkImportAsync<T>(...)` (optional linq2db typed bulk import)
- `BeginTransactionAsync(...)` → `TransactionHandle`

---

## 4) Fastest Way (Ordered)

### Reads (fastest → easiest)

1. `ExecuteReaderAsync` + manual `reader.Get*` (lowest overhead)
2. `StreamAsync` + `IDataRecord` mapping (low overhead, easy cancellation)
3. `DbExecutorQueryExtensions.QueryAsync<T>` (same streaming, adds mapping convenience)
4. `QueryTableAsync` (buffers into a `DataTable`)
5. `ExecuteDataSetAsync` / `QueryTablesAsync` (buffers multiple tables)

### Writes (fastest → most flexible)

1. `BulkImportAsync` (provider bulk path: `SqlBulkCopy` / `COPY BINARY` / `OracleBulkCopy`)
2. `BulkImportAsync<T>` (linq2db typed bulk; convenience-oriented)
3. `ExecuteAsync` (simple non-query)
4. `QueryTableAsync` for stored procedures that return **only output parameters** (returns empty table + outputs)

---

## 5) Input + Output Parameters (Complete Examples)

### 5.1 Input parameters (all providers)

```csharp
using System.Data;
using AdoAsync;
using AdoAsync.Execution;

await using var executor = DbExecutor.Create(new DbOptions
{
    DatabaseType = DatabaseType.SqlServer,
    ConnectionString = "...",
    CommandTimeoutSeconds = 30
});

(int RowsAffected, IReadOnlyDictionary<string, object?> OutputParameters) affected =
    await executor.ExecuteAsync(new CommandDefinition
{
    CommandText = "update dbo.Items set Name = @name where Id = @id",
    CommandType = CommandType.Text,
    Parameters = new[]
    {
        new DbParameter { Name = "@id", DataType = DbDataType.Int32, Direction = ParameterDirection.Input, Value = 42 },
        new DbParameter { Name = "@name", DataType = DbDataType.String, Direction = ParameterDirection.Input, Value = "NewName" }
    }
});

int rowsAffected = affected.RowsAffected;
```

### 5.2 Output parameters (complete examples)

#### A) Non-query + outputs (all providers; tuple return)

```csharp
using System.Collections.Generic;
using System.Data;
using AdoAsync;
using AdoAsync.Execution;

(int RowsAffected, IReadOnlyDictionary<string, object?> OutputParameters) result =
    await executor.ExecuteAsync(new CommandDefinition
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

int rowsAffected = result.RowsAffected;
int? status = (int?)result.OutputParameters["status"];
string? message = (string?)result.OutputParameters["message"];
```

#### B) Buffered rows + outputs (all providers; read from `ExtendedProperties`)

Use `QueryTableAsync` or `ExecuteDataSetAsync` and read output parameters from the returned tuple:

```csharp
using System.Data;
using AdoAsync;
using AdoAsync.Execution;

(DataTable Table, IReadOnlyDictionary<string, object?> OutputParameters) tableResult =
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

var status = (int?)tableResult.OutputParameters["status"];
var message = (string?)tableResult.OutputParameters["message"];
```

Expected output shape (example):

- `status` → `1`
- `message` → `"OK"`

### 5.3 Output parameters (streaming path; SQL Server/PostgreSQL only)

```csharp
using System.Data;
using AdoAsync;
using AdoAsync.Execution;

await using var result = await executor.ExecuteReaderWithOutputsAsync(new CommandDefinition
{
    CommandText = "dbo.SelectCustomersAndReturnTotal",
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

## 6) Transactions (Explicit + Rollback-on-dispose)

```csharp
await using var executor = DbExecutor.Create(options);
await using var tx = await executor.BeginTransactionAsync();

(int RowsAffected, IReadOnlyDictionary<string, object?> OutputParameters) _ =
    await executor.ExecuteAsync(new CommandDefinition { CommandText = "update ..." });
(int RowsAffected, IReadOnlyDictionary<string, object?> OutputParameters) __ =
    await executor.ExecuteAsync(new CommandDefinition { CommandText = "insert ..." });

await tx.CommitAsync(); // if omitted, DisposeAsync rolls back
```

Notes:

- Retries are skipped while a user transaction is active.
- Bulk imports also enlist in the active executor transaction.

---

## 7) Bulk Import (Provider Fast Paths + Optional linq2db)

### 7.1 Provider fast path (`BulkImportAsync(BulkImportRequest)`)

The `BulkImportRequest` is allow-list driven (recommended to keep it enabled in production):

```csharp
using var sourceReader = GetReader(); // DbDataReader with columns: Id, Name

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
    BatchSize = 10_000
};

var result = await executor.BulkImportAsync(request);
if (!result.Success)
{
    throw new Exception(result.Error!.MessageKey);
}

Console.WriteLine($"Rows={result.RowsInserted}, Duration={result.Duration}");
```

### 7.2 Typed bulk (`BulkImportAsync<T>`) via linq2db (optional)

This path is disabled unless `DbOptions.LinqToDb.Enable = true`.

```csharp
var options = new DbOptions
{
    DatabaseType = DatabaseType.SqlServer,
    ConnectionString = "...",
    CommandTimeoutSeconds = 30,
    LinqToDb = new LinqToDbBulkOptions { Enable = true }
};

await using var executor = DbExecutor.Create(options);

var items = new[]
{
    new MyRow { Id = 1, Name = "A" },
    new MyRow { Id = 2, Name = "B" }
};

var result = await executor.BulkImportAsync(items, tableName: "dbo.Items");
```

---

## 8) Extensions Used (How Many + What They Do)

This repo contains **12** extension classes (`static class ...Extensions`).

### Public extensions (caller-facing)

- `AdoAsync.DependencyInjection.AdoAsyncServiceCollectionExtensions`
  - DI registration (`AddAdoAsyncFactory`, `AddAdoAsync`, `AddAdoAsyncExecutor`)
- `AdoAsync.Common.DataRecordExtensions`
  - `IDataRecord.Get<T>(ordinal/name)` (typed getter + conversions)
- `AdoAsync.Common.DataTableExtensions`
  - DataTable helpers used by callers (CSV/data helpers in Common project)
- `AdoAsync.Common.CsvExtensions`, `AdoAsync.Common.FileReadExtensions`

### Internal extensions (implementation-only)

Used for mapping performance and normalization inside the library:

- `AdoAsync.Extensions.Execution.DbDataReaderExtensions`
- `AdoAsync.Extensions.Execution.DataTableExtensions` (internal)
- `AdoAsync.Extensions.Execution.DataSetExtensions` (internal)
- `AdoAsync.Extensions.Execution.MultiResultMapExtensions`
- `AdoAsync.Extensions.Execution.SpanMappingExtensions`
- `AdoAsync.Extensions.Execution.ValueNormalizationExtensions`
- `AdoAsync.Extensions.Execution.NullHandlingExtensions`

---

## 9) Provider-Specific Docs (Detailed)

See:

- `docs/providers/sqlserver.md`
- `docs/providers/postgresql.md`
- `docs/providers/oracle.md`

And:

- `docs/type-handling.md`
- `docs/linq2db.md` (typed bulk copy)
- `docs/bulk-update.md` (bulk update/upsert pattern)
