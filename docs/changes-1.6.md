# AdoAsync v1.6 – Change Log (Draft)

Grouped by action. Each item includes `file:line` and method signature (with return type).

## Added

- `src/AdoAsync/Abstractions/IDbExecutor.cs:21` `ValueTask<StreamingReaderResult> ExecuteReaderAsync(CommandDefinition command, CancellationToken cancellationToken = default);`
- `src/AdoAsync/Abstractions/IDbExecutor.cs:93` `ValueTask<TransactionHandle> BeginTransactionAsync(CancellationToken cancellationToken = default);`
- `src/AdoAsync/Execution/Async/DbExecutor.Transactions.cs:11` `public async ValueTask<TransactionHandle> BeginTransactionAsync(CancellationToken cancellationToken = default)`
- `src/AdoAsync/Extensions/Normalization/DbValueNormalizationExtensions.cs` (consolidated normalization entrypoint)

## Changed (API return values)

- `src/AdoAsync/Abstractions/IDbExecutor.cs:42` `ValueTask<(T Value, IReadOnlyDictionary<string, object?> OutputParameters)> ExecuteScalarAsync<T>(...)`
- `src/AdoAsync/Abstractions/IDbExecutor.cs:53` `ValueTask<(int RowsAffected, IReadOnlyDictionary<string, object?> OutputParameters)> ExecuteAsync(...)`
- `src/AdoAsync/Abstractions/IDbExecutor.cs:61` `ValueTask<(DataTable Table, IReadOnlyDictionary<string, object?> OutputParameters)> QueryTableAsync(...)`
- `src/AdoAsync/Abstractions/IDbExecutor.cs:70` `ValueTask<(IReadOnlyList<DataTable> Tables, IReadOnlyDictionary<string, object?> OutputParameters)> QueryTablesAsync(...)`
- `src/AdoAsync/Abstractions/IDbExecutor.cs:78` `ValueTask<(List<T> Rows, IReadOnlyDictionary<string, object?> OutputParameters)> QueryAsync<T>(...)`
- `src/AdoAsync/Abstractions/IDbExecutor.cs:88` `ValueTask<(DataSet DataSet, IReadOnlyDictionary<string, object?> OutputParameters)> ExecuteDataSetAsync(...)`

## Changed (Executor methods / locations)

- `src/AdoAsync/Execution/Async/DbExecutor.cs:87` `public async ValueTask<StreamingReaderResult> ExecuteReaderAsync(CommandDefinition command, CancellationToken cancellationToken = default)`
- `src/AdoAsync/Execution/Async/DbExecutor.cs:100` `public async IAsyncEnumerable<IDataRecord> StreamAsync(CommandDefinition command, CancellationToken cancellationToken = default)`
- `src/AdoAsync/Execution/Async/DbExecutor.cs:154` `public async ValueTask<(int RowsAffected, IReadOnlyDictionary<string, object?> OutputParameters)> ExecuteAsync(...)`
- `src/AdoAsync/Execution/Async/DbExecutor.cs:182` `public async ValueTask<(T Value, IReadOnlyDictionary<string, object?> OutputParameters)> ExecuteScalarAsync<T>(...)`
- `src/AdoAsync/Execution/Async/DbExecutor.cs:223` `public async ValueTask<(DataTable Table, IReadOnlyDictionary<string, object?> OutputParameters)> QueryTableAsync(...)`
- `src/AdoAsync/Execution/Async/DbExecutor.cs:258` `public async ValueTask<(IReadOnlyList<DataTable> Tables, IReadOnlyDictionary<string, object?> OutputParameters)> QueryTablesAsync(...)`
- `src/AdoAsync/Execution/Async/DbExecutor.cs:292` `public async ValueTask<(List<T> Rows, IReadOnlyDictionary<string, object?> OutputParameters)> QueryAsync<T>(...)`
- `src/AdoAsync/Execution/Async/DbExecutor.cs:318` `public async ValueTask<(DataSet DataSet, IReadOnlyDictionary<string, object?> OutputParameters)> ExecuteDataSetAsync(...)`

## Changed (Query convenience)

- `src/AdoAsync/Execution/DbExecutorQueryExtensions.cs:14` `public static IAsyncEnumerable<T> QueryAsync<T>(this IDbExecutor executor, CommandDefinition command, Func<IDataRecord, T> map, CancellationToken cancellationToken = default)`
  - Streaming mapping convenience stays as an extension method (built on `IDbExecutor.StreamAsync`).

## Changed (Output parameter rules)

- `src/AdoAsync/Helpers/ParameterHelper.cs:15` `public static IReadOnlyDictionary<string, object?>? ExtractOutputParameters(DbCommand command, IReadOnlyList<DbParameter>? parameters)`
  - Only exposes `Output` / `InputOutput` parameters.
  - Skips `DbDataType.RefCursor` (handled as result sets).
  - Skips `ParameterDirection.ReturnValue`.
- `src/AdoAsync/Validation/DbParameterValidator.cs:7` `public sealed class DbParameterValidator : AbstractValidator<DbParameter>`
  - Disallows `ParameterDirection.ReturnValue`.

## Changed (Refcursor policy)

- `src/AdoAsync/Execution/Async/DbExecutor.RefCursor.cs:30` `private async ValueTask<T> WithTransactionScopeAsync<T>(CancellationToken cancellationToken, Func<DbTransaction, Task<T>> action)`
  - Reuses active user transaction if present; otherwise creates a local transaction for PostgreSQL refcursor fetching.
- `src/AdoAsync/Execution/Async/DbExecutor.RefCursor.cs:60` `private async ValueTask<(IReadOnlyList<DataTable> Tables, IReadOnlyDictionary<string, object?> OutputParameters)> ExecuteOracleRefCursorsWithOutputsAsync(...)`
  - Policy: do not retry refcursor stored procedures.
- `src/AdoAsync/Execution/Async/DbExecutor.RefCursor.cs:87` `private async ValueTask<(IReadOnlyList<DataTable> Tables, IReadOnlyDictionary<string, object?> OutputParameters)> ExecutePostgresRefCursorsWithOutputsAsync(...)`
  - Policy: do not retry (refcursor requires transaction scope).

## Changed (Normalization helpers)

- `src/AdoAsync/Extensions/Normalization/DbValueNormalizationExtensions.cs:137` `public static object? NormalizeByType(this object? value, DbDataType dataType)`
- `src/AdoAsync/Extensions/Normalization/DbValueNormalizationExtensions.cs:163` `public static T? NormalizeAsNullable<T>(this object? value, DbDataType dataType) where T : struct`
- `src/AdoAsync/Extensions/Normalization/DbValueNormalizationExtensions.cs:8` `internal static class DbValueNormalizer`
  - Internal normalizer + public extensions consolidated into a single file under the Extensions folder.
  - Normalizes `DbDataType.Timestamp` as binary when possible.

## Performance / Allocation

- `src/AdoAsync/Execution/Async/DbExecutor.Infrastructure.cs:36` `private async ValueTask EnsureConnectionAsync(CancellationToken cancellationToken)`
  - Centralized disposed guard at the connection boundary (no extra helper method).
- `src/AdoAsync/Helpers/ParameterHelper.cs:15` `ExtractOutputParameters(...)`
  - Uses a single `Dictionary` for declared parameter lookup (reduced allocations).

## Documentation / Ownership (XML docs)

- Enforced lifetime/ownership notes on conversion methods:
  - `src/AdoAsync/Extensions/DataReader/DbDataReaderExtensions.cs:34` `StreamRecordsAsync(...)`
  - `src/AdoAsync/Extensions/DataTable/DataTableExtensions.cs:31` `ToList<T>(...)`
  - `src/AdoAsync/Extensions/DataTable/DataTableExtensions.cs:66` `ToArray<T>(...)`
  - `src/AdoAsync/Extensions/AsyncEnumerable/AsyncEnumerableMaterializerExtensions.cs:43` `ToListAsync<T>(...)`
  - `src/AdoAsync/Extensions/AsyncEnumerable/AsyncEnumerableMaterializerExtensions.cs:76` `ToArrayAsync<T>(...)`
  - `src/AdoAsync/Extensions/AsyncEnumerable/AsyncEnumerableMaterializerExtensions.cs:102` `ToFrozenDictionaryAsync<TSource, TKey, TValue>(...)`
  - `src/AdoAsync/Extensions/AsyncEnumerable/AsyncEnumerableMaterializerExtensions.cs:147` `ToLookupAsync<TSource, TKey, TElement>(...)`
  - `Documentation/ConversionGuidelines.md` (rules + ownership + prompt template)
  - `Documentation/RulesCheatSheet.md` (one-page rules)
  - `Documentation/CodexPromptTemplate.md` (reusable Codex prompt)

## Refactor / Structure

- Executor split into feature-based partials:
  - `src/AdoAsync/Execution/Async/DbExecutor.cs` (streaming + execute + buffered query + dataset)
  - `src/AdoAsync/Execution/Async/DbExecutor.Transactions.cs` (explicit transactions)
  - `src/AdoAsync/Execution/Async/DbExecutor.Bulk.cs` (bulk import)
  - `src/AdoAsync/Execution/Async/DbExecutor.Infrastructure.cs` (connection/validation/retry/error helpers)
  - `src/AdoAsync/Execution/Async/DbExecutor.RefCursor.cs` (Oracle/PostgreSQL refcursor paths)
- Extensions reorganized into category folders under `src/AdoAsync/Extensions/` (namespaces remain `AdoAsync.Extensions.Execution`):
  - `src/AdoAsync/Extensions/DataReader/*`
  - `src/AdoAsync/Extensions/DataTable/*`
  - `src/AdoAsync/Extensions/AsyncEnumerable/*`
  - `src/AdoAsync/Extensions/Collections/*`
  - `src/AdoAsync/Extensions/Normalization/*`

## Moved

- `src/AdoAsync/Extensions/Execution/DbDataReaderExtensions.cs` → `src/AdoAsync/Extensions/DataReader/DbDataReaderExtensions.cs`
- `src/AdoAsync/Extensions/Execution/DataTableExtensions.cs` → `src/AdoAsync/Extensions/DataTable/DataTableExtensions.cs`
- `src/AdoAsync/Extensions/DataTable/MultiResultMapExtensions.cs` → `src/AdoAsync/Extensions/DataTable/DataSetMapExtensions.cs`
- `src/AdoAsync/Extensions/Execution/SpanMappingExtensions.cs` → `src/AdoAsync/Extensions/Collections/SpanMappingExtensions.cs`
- `src/AdoAsync/Extensions/Execution/ValueNormalizationExtensions.cs` → `src/AdoAsync/Extensions/Normalization/DbValueNormalizationExtensions.cs`
- `src/AdoAsync/Extensions/Execution/NullHandlingExtensions.cs` → `src/AdoAsync/Extensions/Normalization/DbValueNormalizationExtensions.cs`

## Removed

- `src/AdoAsync/Execution/Async/DbExecutor.Streaming.cs` (moved into `DbExecutor.cs`)
- `src/AdoAsync/Execution/Async/DbExecutor.ExecutionAndQuery.cs` (duplicate partial removed)
- `src/AdoAsync/Execution/OutputParameterConverter.cs` (renamed/replaced by internal normalizer; public API remains `NormalizeByType`).
- `src/AdoAsync/Execution/Async/CommandOwningDbDataReader.cs` (unused wrapper; `StreamingReaderResult` owns command+reader lifetime)
- `src/AdoAsync/Core/MultiResult.cs` (removed; use `DataSet`/`DataTable` directly)
- `src/AdoAsync/Extensions/DataTable/DataSetExtensions.cs` (removed; `MultiResult` wrapper not used)
- `src/AdoAsync/Execution/DbValueNormalizer.cs` (consolidated into `src/AdoAsync/Extensions/Normalization/DbValueNormalizationExtensions.cs`)
- `src/AdoAsync/Extensions/Normalization/ValueNormalizationExtensions.cs` (consolidated)
- `src/AdoAsync/Extensions/Normalization/NullHandlingExtensions.cs` (consolidated)
- `src/AdoAsync/Extensions/Execution/DataTableOutputExtensions.cs` (removed)
- `src/AdoAsync/Extensions/Execution/DataSetOutputExtensions.cs` (removed)
- `src/AdoAsync/Helpers/CursorHelper.cs` `CollectPostgresCursorNames(...)` (removed)
