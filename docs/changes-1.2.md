# Changes 1.2

## Overview
This release keeps the API surface stable while clarifying how to retrieve output parameters from stored procedures. The interface now exposes the existing `QueryTablesAsync` method so callers can get output parameters without extra wrappers. No behavioral regressions were introduced.

## What changed
- `IDbExecutor` now includes `QueryTablesAsync` (already implemented in `DbExecutor`), exposing the standard `DbResult` that carries `OutputParameters` alongside buffered tables.
- No new execution methods were added; existing behavior for streaming, scalar, and multi-result flows remains unchanged.
- Documentation was updated to reflect the output-parameter path.

## Output parameter usage
- Use `QueryMultipleAsync` when you need multi-result sets and output parameters together.
- Use `QueryTablesAsync` (now part of the interface) to get a `DbResult` with both buffered tables and `OutputParameters` for stored procedures.
- `ExecuteScalarAsync` continues to support output parameters for scalar scenarios; streaming methods remain input-only.

## Feature comparison (before vs now)
| Area | Before | Now |
| --- | --- | --- |
| Output params with buffered results | `QueryTablesAsync` was available on `DbExecutor` only (not on the interface). | `QueryTablesAsync` is on `IDbExecutor`; same behavior, now discoverable and contractually supported. |
| Multi-result with outputs | `QueryMultipleAsync` returned `MultiResult` with `OutputParameters`. | Unchanged. |
| Streaming methods | Input-only; no output params. | Unchanged. |

## References
- Method matrix: `docs/IDbExecutor-methods.md`
- Core result contract: `src/AdoAsync/Core/DbResult.cs`

## Performance tweaks
- Pre-sized output-parameter dictionary allocation to current parameter count to avoid rehashing under many outputs.
- Dependency injection lookup map now uses FrozenDictionary for faster, allocation-free reads (src/AdoAsync/Extensions/DependencyInjection/DbExecutorFactory.cs:1-58).
