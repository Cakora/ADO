# Changes 1.5 (Non-Markdown Only)

This changelog lists **non-`.md`** changes for 1.5 (code/scripts only).

## Added
- src/AdoAsync/Execution/DbExecutorQueryExtensions.cs

## Updated
- src/AdoAsync/Execution/Async/DbExecutor.cs (tuple-return output parameters across IDbExecutor methods)
- src/AdoAsync/Execution/Async/DbExecutor.RefCursor.cs
- src/AdoAsync/Execution/Async/CommandOwningDbDataReader.cs
- src/AdoAsync/Abstractions/IDbExecutor.cs (tuple-return output parameters across buffered methods)
- src/AdoAsync/Abstractions/StreamingReaderResult.cs:24 (constructor signature update)
- src/AdoAsync/Core/CommandDefinition.cs
- examples/AdoAsync.Demo/Program.cs:1
- src/AdoAsync/Extensions/DataReader/DbDataReaderExtensions.cs
- src/AdoAsync/Extensions/DataTable/DataTableExtensions.cs
- src/AdoAsync/Extensions/DataTable/DataSetExtensions.cs
- src/AdoAsync/Extensions/DataTable/MultiResultMapExtensions.cs
- src/AdoAsync/Extensions/Collections/SpanMappingExtensions.cs
- src/AdoAsync/Extensions/Normalization/ValueNormalizationExtensions.cs
- src/AdoAsync/Extensions/Normalization/NullHandlingExtensions.cs
- docs/sql-scripts/bulk-upsert-by-name-postgresql.sql

## New API Added
- src/AdoAsync/Execution/DbExecutorQueryExtensions.cs:17 `IAsyncEnumerable<T> DbExecutorQueryExtensions.QueryAsync<T>(this IDbExecutor executor, CommandDefinition command, Func<IDataRecord, T> map, CancellationToken cancellationToken = default)`
- src/AdoAsync/Abstractions/IDbExecutor.cs `ValueTask<(T Value, IReadOnlyDictionary<string, object?> OutputParameters)> ExecuteScalarAsync<T>(...)`
- src/AdoAsync/Abstractions/IDbExecutor.cs `ValueTask<(int RowsAffected, IReadOnlyDictionary<string, object?> OutputParameters)> ExecuteAsync(...)`
- src/AdoAsync/Abstractions/IDbExecutor.cs `ValueTask<(DataTable Table, IReadOnlyDictionary<string, object?> OutputParameters)> QueryTableAsync(...)`
- src/AdoAsync/Abstractions/IDbExecutor.cs `ValueTask<(List<T> Rows, IReadOnlyDictionary<string, object?> OutputParameters)> QueryAsync<T>(...)`
- src/AdoAsync/Abstractions/IDbExecutor.cs `ValueTask<(DataSet DataSet, IReadOnlyDictionary<string, object?> OutputParameters)> ExecuteDataSetAsync(...)`

## Moved
- (none)

## Deleted
- src/AdoAsync/Core/OutputParameterCapture.cs
