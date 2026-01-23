# Changes 1.5 (Non-Markdown Only)

This changelog lists **non-`.md`** changes for 1.5 (code/scripts only).

## Added
- src/AdoAsync/Execution/DbExecutorQueryExtensions.cs

## Updated
- src/AdoAsync/Execution/Async/DbExecutor.cs:335
- src/AdoAsync/Abstractions/IDbExecutor.cs:31
- src/AdoAsync/README.md:49
- examples/AdoAsync.Demo/Program.cs:1
- src/AdoAsync/Extensions/Execution/DbDataReaderExtensions.cs
- src/AdoAsync/Extensions/Execution/DataTableExtensions.cs
- src/AdoAsync/Extensions/Execution/DataSetExtensions.cs
- src/AdoAsync/Extensions/Execution/MultiResultMapExtensions.cs
- src/AdoAsync/Extensions/Execution/SpanMappingExtensions.cs
- src/AdoAsync/Extensions/Execution/ValueNormalizationExtensions.cs
- src/AdoAsync/Extensions/Execution/NullHandlingExtensions.cs
- docs/sql-scripts/bulk-upsert-by-name-postgresql.sql

## New API Added
- src/AdoAsync/Execution/DbExecutorQueryExtensions.cs:17 `IAsyncEnumerable<T> DbExecutorQueryExtensions.QueryAsync<T>(this IDbExecutor executor, CommandDefinition command, Func<IDataRecord, T> map, CancellationToken cancellationToken = default)`

## Moved
- (none)

## Deleted
- (none)
