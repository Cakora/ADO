# Type Handling Across Providers

## Goal
Keep a single error contract and predictable data shapes across SQL Server, PostgreSQL, and Oracle, while acknowledging provider-specific return types.

## Known Provider Differences
- Oracle often returns numeric output parameters as `decimal` (even when logical type is `Int64`).
- PostgreSQL/Oracle use refcursor outputs for multi-result stored procedures.
- `DataTable`/`DataSet` materialization preserves provider-native types.
- `IDataReader` mapping returns provider-native types unless the caller converts.

## Current Handling (Centralized)
- Output parameters are normalized in one place: `src/AdoAsync/Execution/OutputParameterConverter.cs`.
- Output names are normalized by trimming parameter prefixes in `src/AdoAsync/Execution/Async/DbExecutor.cs`.
- Multi-result procedures are handled via provider-owned refcursor paths.
- Reader mapping is explicit (`QueryAsync<T>` uses typed getters by ordinal).
- Optional normalization helpers live in `src/AdoAsync.Common/` for readers and DataTables.

## Plan (Step by Step)
1) Identify the boundary where normalization is required (output params, scalar results, or reader values).
2) Use a single normalization helper for that boundary to avoid per-provider duplication.
3) Keep provider-specific exceptions and refcursor mechanics in provider folders only.
4) Add tests that document the expected cross-provider shape (e.g., decimal → Int64 outputs).
5) Document known differences in this file and point callers to explicit mapping patterns.

## Where Code Should Live
- Cross-provider normalization: `src/AdoAsync/Execution/OutputParameterConverter.cs`
- Provider-specific behavior: `src/AdoAsync/Providers/*`
- Error contract and mapping: `src/AdoAsync/Core/DbError.cs`, `src/AdoAsync/Core/DbErrorMapper.cs`
- Reader mapping rules: call sites (explicit mappers) or a future shared mapper in `src/AdoAsync.Common`

## Guidance for Callers
- Prefer `QueryAsync<T>` with typed getters for consistent mapping.
- Use `DbResult.OutputParameters` for output values; they are normalized by `DbDataType`.
- Expect `DataTable`/`DataSet` to keep provider types; convert explicitly if needed.
- Use `AdoAsync.Common.DataRecordExtensions` for safe typed reads from `IDataRecord`.
- Use `AdoAsync.Common.DataTableExtensions.Normalize` when you need a uniform type shape (pass CLR `Type` per column).

### Example: Normalize a DataTable
```csharp
using AdoAsync.Common;

var normalized = table.Normalize(new Dictionary<string, Type>
{
    ["Id"] = typeof(long),
    ["CreatedAt"] = typeof(DateTimeOffset)
});
```
