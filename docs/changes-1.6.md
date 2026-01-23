# AdoAsync v1.6 – Change Log (Draft)

Grouped by action. Each item includes `file:line` and method signature (with return type).

## Added

- `src/AdoAsync/Abstractions/IDbExecutor.cs:27` `ValueTask<StreamingReaderResult> ExecuteReaderWithOutputsAsync(CommandDefinition command, CancellationToken cancellationToken = default);`
- `src/AdoAsync/Execution/Async/DbExecutor.ExecutionAndQuery.cs:40` `public async ValueTask<StreamingReaderResult> ExecuteReaderWithOutputsAsync(CommandDefinition command, CancellationToken cancellationToken = default)`
- `src/AdoAsync/Abstractions/IDbExecutor.cs:93` `ValueTask<TransactionHandle> BeginTransactionAsync(CancellationToken cancellationToken = default);`
- `src/AdoAsync/Execution/Async/DbExecutor.Transactions.cs:11` `public async ValueTask<TransactionHandle> BeginTransactionAsync(CancellationToken cancellationToken = default)`

## Changed (API return values)

- `src/AdoAsync/Abstractions/IDbExecutor.cs:42` `ValueTask<(T Value, IReadOnlyDictionary<string, object?> OutputParameters)> ExecuteScalarAsync<T>(...)`
- `src/AdoAsync/Abstractions/IDbExecutor.cs:53` `ValueTask<(int RowsAffected, IReadOnlyDictionary<string, object?> OutputParameters)> ExecuteAsync(...)`
- `src/AdoAsync/Abstractions/IDbExecutor.cs:61` `ValueTask<(DataTable Table, IReadOnlyDictionary<string, object?> OutputParameters)> QueryTableAsync(...)`
- `src/AdoAsync/Abstractions/IDbExecutor.cs:70` `ValueTask<(IReadOnlyList<DataTable> Tables, IReadOnlyDictionary<string, object?> OutputParameters)> QueryTablesAsync(...)`
- `src/AdoAsync/Abstractions/IDbExecutor.cs:78` `ValueTask<(List<T> Rows, IReadOnlyDictionary<string, object?> OutputParameters)> QueryAsync<T>(...)`
- `src/AdoAsync/Abstractions/IDbExecutor.cs:88` `ValueTask<(DataSet DataSet, IReadOnlyDictionary<string, object?> OutputParameters)> ExecuteDataSetAsync(...)`

## Changed (Executor methods / locations)

- `src/AdoAsync/Execution/Async/DbExecutor.ExecutionAndQuery.cs:18` `public async ValueTask<DbDataReader> ExecuteReaderAsync(...)`
- `src/AdoAsync/Execution/Async/DbExecutor.ExecutionAndQuery.cs:72` `public async IAsyncEnumerable<IDataRecord> StreamAsync(...)`
- `src/AdoAsync/Execution/Async/DbExecutor.ExecutionAndQuery.cs:89` `public async ValueTask<(int RowsAffected, IReadOnlyDictionary<string, object?> OutputParameters)> ExecuteAsync(...)`
- `src/AdoAsync/Execution/Async/DbExecutor.ExecutionAndQuery.cs:117` `public async ValueTask<(T Value, IReadOnlyDictionary<string, object?> OutputParameters)> ExecuteScalarAsync<T>(...)`
- `src/AdoAsync/Execution/Async/DbExecutor.ExecutionAndQuery.cs:158` `public async ValueTask<(DataTable Table, IReadOnlyDictionary<string, object?> OutputParameters)> QueryTableAsync(...)`
- `src/AdoAsync/Execution/Async/DbExecutor.ExecutionAndQuery.cs:193` `public async ValueTask<(IReadOnlyList<DataTable> Tables, IReadOnlyDictionary<string, object?> OutputParameters)> QueryTablesAsync(...)`
- `src/AdoAsync/Execution/Async/DbExecutor.ExecutionAndQuery.cs:227` `public async ValueTask<(List<T> Rows, IReadOnlyDictionary<string, object?> OutputParameters)> QueryAsync<T>(...)`
- `src/AdoAsync/Execution/Async/DbExecutor.ExecutionAndQuery.cs:253` `public async ValueTask<(DataSet DataSet, IReadOnlyDictionary<string, object?> OutputParameters)> ExecuteDataSetAsync(...)`

## Changed (Output parameter rules)

- `src/AdoAsync/Helpers/ParameterHelper.cs:15` `public static IReadOnlyDictionary<string, object?>? ExtractOutputParameters(DbCommand command, IReadOnlyList<DbParameter>? parameters)`
  - Only exposes `Output` / `InputOutput` parameters.
  - Skips `DbDataType.RefCursor` (handled as result sets).
  - Skips `ParameterDirection.ReturnValue`.
- `src/AdoAsync/Validation/DbParameterValidator.cs:7` `public sealed class DbParameterValidator : AbstractValidator<DbParameter>`
  - Disallows `ParameterDirection.ReturnValue`.

## Changed (Refcursor policy)

- `src/AdoAsync/Execution/Async/DbExecutor.RefCursor.cs:35` `private async ValueTask<T> WithTransactionScopeAsync<T>(CancellationToken cancellationToken, Func<DbTransaction, Task<T>> action)`
  - Reuses active user transaction if present; otherwise creates a local transaction for PostgreSQL refcursor fetching.
- `src/AdoAsync/Execution/Async/DbExecutor.RefCursor.cs:63` `private async ValueTask<(IReadOnlyList<DataTable> Tables, IReadOnlyDictionary<string, object?> OutputParameters)> ExecuteOracleRefCursorsWithOutputsAsync(...)`
  - Policy: do not retry refcursor stored procedures.
- `src/AdoAsync/Execution/Async/DbExecutor.RefCursor.cs:91` `private async ValueTask<(IReadOnlyList<DataTable> Tables, IReadOnlyDictionary<string, object?> OutputParameters)> ExecutePostgresRefCursorsWithOutputsAsync(...)`
  - Policy: do not retry (refcursor requires transaction scope).

## Changed (Normalization helpers)

- `src/AdoAsync/Extensions/Execution/ValueNormalizationExtensions.cs:137` `public static object? NormalizeByType(this object? value, DbDataType dataType)`
- `src/AdoAsync/Extensions/Execution/ValueNormalizationExtensions.cs:147` `public static T? NormalizeAsNullable<T>(this object? value, DbDataType dataType) where T : struct`

## Performance / Allocation

- `src/AdoAsync/Execution/Async/DbExecutor.Infrastructure.cs:82` `private ValueTask EnsureNotDisposedAsync()`
  - No async state-machine allocation on the hot path (returns `ValueTask.CompletedTask`).
- `src/AdoAsync/Helpers/ParameterHelper.cs:15` `ExtractOutputParameters(...)`
  - Uses a single `Dictionary` for declared parameter lookup (reduced allocations).

## Refactor / Structure

- Executor split into feature-based partials:
  - `src/AdoAsync/Execution/Async/DbExecutor.ExecutionAndQuery.cs` (streaming + execute + buffered query)
  - `src/AdoAsync/Execution/Async/DbExecutor.Transactions.cs` (explicit transactions)
  - `src/AdoAsync/Execution/Async/DbExecutor.Bulk.cs` (bulk import)
  - `src/AdoAsync/Execution/Async/DbExecutor.Infrastructure.cs` (connection/validation/retry/error helpers)
  - `src/AdoAsync/Execution/Async/DbExecutor.RefCursor.cs` (Oracle/PostgreSQL refcursor paths)

## Removed

- `src/AdoAsync/Execution/OutputParameterConverter.cs` (renamed/replaced by internal normalizer; public API remains `NormalizeByType`).
- `src/AdoAsync/Extensions/Execution/DataTableOutputExtensions.cs` (removed)
- `src/AdoAsync/Extensions/Execution/DataSetOutputExtensions.cs` (removed)
- `src/AdoAsync/Helpers/CursorHelper.cs` `CollectPostgresCursorNames(...)` (removed)

