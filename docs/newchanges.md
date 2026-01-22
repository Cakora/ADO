# Recent Changes Summary

## New files
- `docs/IDbExecutor-methods.md` — capability matrix for IDbExecutor methods with provider/streaming support markers.
- `src/AdoAsync/Core/MultiResult.cs` — structured multi-result payload (tables + output parameters) with XML docs.
- `src/AdoAsync/Execution/Async/DbExecutor.RefCursor.cs` — partial class housing refcursor-specific routing/logic for readability.
- `src/AdoAsync/Extensions/Execution/DataTableExtensions.cs` — buffered DataTable → List mapping (indexed iteration for performance).
- `src/AdoAsync/Extensions/Execution/DataSetExtensions.cs` — DataSet helpers for MultiResult conversion and table access.
- `src/AdoAsync/Extensions/Execution/DbDataReaderExtensions.cs` — streaming reader helpers (IAsyncEnumerable, typed getters).
- `src/AdoAsync/Extensions/Execution/MultiResultMapExtensions.cs` — buffered multi-result mapping helpers (lists/arrays/collection factories).
- `src/AdoAsync/Extensions/Execution/NullHandlingExtensions.cs` — DBNull → null helper.
- `src/AdoAsync/Extensions/Execution/SpanMappingExtensions.cs` — span-based array projection helper.
- `src/AdoAsync/Extensions/Execution/ValueNormalizationExtensions.cs` — cross-provider value normalization helpers.
- `src/AdoAsync/Helpers/CursorHelper.cs` — refcursor detection/collection helpers.
- `src/AdoAsync/Helpers/DataAdapterHelper.cs` — provider DataAdapter fill helpers (DataTable/DataSet/tables).
- `src/AdoAsync/Helpers/ParameterHelper.cs` — output-parameter extraction helper.
- `src/AdoAsync/Helpers/ProviderHelper.cs` — provider resolution/error mapping helper.

## Updated files (notable changes)
- `src/AdoAsync/Abstractions/IDbExecutor.cs` — finalized async-only executor surface with XML docs.
- `src/AdoAsync/Execution/Async/DbExecutor.cs` — routed buffered paths through DataAdapter helpers, blocked Oracle streaming, error mapping tweaks.
- `src/AdoAsync/Providers/*ExceptionMapper.cs` — fallback mapping now uses DbErrorMapper.Map.
- `src/AdoAsync/Validation/DbOptionsValidator.cs` — CommandTimeoutSeconds must be positive.
- `tests/AdoAsync.Tests/QueryTablesIntegrationTests.cs` — buffered QueryAsync usage aligned with DataTable mapping.
- `src/AdoAsync/Common/DataTableExtensions.cs` and `src/AdoAsync.Common/CsvExtensions.cs` — indexed loops for DataRow iteration.
