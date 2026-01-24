# Helper Inventory (AdoAsync)

This file is a living inventory of conversion/normalization helpers. Populate before replacing/removing anything.

## Current helpers (source of truth: `src/AdoAsync/Extensions/*` and `src/AdoAsync/Helpers/*`)

### Streaming / Reader
- `AdoAsync.Extensions.Execution.DbDataReaderExtensions.StreamRecordsAsync(DbDataReader, CancellationToken)`
- `AdoAsync.Extensions.Execution.DbDataReaderExtensions.StreamAsync<T>(DbDataReader, Func<IDataRecord,T>, CancellationToken)`

### Buffered / DataTable / DataSet
- `AdoAsync.Extensions.Execution.DataTableExtensions.ToList<T>(DataTable, Func<DataRow,T>)`
- `AdoAsync.Extensions.Execution.DataSetMapExtensions` (DataSet → `List<T>`/arrays/collections)

### Normalization
- `AdoAsync.Extensions.Execution.ValueNormalizationExtensions.NormalizeByType(object?, DbDataType)`
- `AdoAsync.Extensions.Execution.ValueNormalizationExtensions.NormalizeAsNullable<T>(object?, DbDataType)`
- `AdoAsync.Extensions.Execution.NullHandlingExtensions.ToNullIfDbNull(object?)`

### Collection transformers (post-materialization)
- `AdoAsync.Extensions.Execution.SpanMappingExtensions.MapToArray<TSource,TDest>(TSource[], Func<TSource,TDest>)`

## Review (against ConversionGuidelines.md)
### `DataSetMapExtensions` (buffered mapping)
- ✅ Aligned: buffered-only, no provider types, mapping is explicit, fast loops (no LINQ per-row).
- ⚠️ Watch-outs: returns `List<T>`/arrays — acceptable because names encode materialization (`Map...`/`Map...ToArrays`), but keep docs loud about memory/disposal.
- ✅ Ownership: docs explicitly state caller owns/disposes tables; converters do not dispose.
- ✅ Grouping rule: no `GroupBy()` usage here (good).
- Recommendation: keep; any future additions should avoid adding “convenience” overloads that hide materialization.
