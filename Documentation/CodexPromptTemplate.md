# Codex Prompt Template — Conversion Extensions

## Mental contract (paste at top of every Codex task)
This library optimizes for memory first, clarity second, speed third.
Streaming is preferred.
Materialization is explicit.
Purpose decides structure.

## Mandatory instructions
Analyze existing extension methods first.
Do not duplicate functionality.
Follow the documented folder structure and categories:
- `src/AdoAsync/Extensions/DataReader` (streaming)
- `src/AdoAsync/Extensions/DataTable` (buffered)
- `src/AdoAsync/Extensions/AsyncEnumerable` (materializers)
- `src/AdoAsync/Extensions/Collections` (post-materialization shaping)
- `src/AdoAsync/Extensions/Normalization` (single entry point)

## Enforced design rules
1) Extension method discovery rule (naming)
- Names must encode source type, target type, and behavior (streaming/buffered/grouped).

2) No default list rule
- No method may return `List<T>` unless the name explicitly says it materializes.

3) Stream → materialize must be explicit
- Streaming extensions return only `IAsyncEnumerable<T>`/`IEnumerable<T>`.
- Streaming methods must never auto-materialize to `List<T>`/`T[]` internally.
- Materialization must be a separate call in AsyncEnumerable materializers.

4) DataSet/DataTable ownership rule
- Any method accepting `DataSet`/`DataTable` must document ownership and whether it disposes input.
- Default: converters do not dispose; caller disposes immediately after conversion.

5) Oracle constraint rule
- Oracle paths are always buffered.
- Do not add async streaming extensions for Oracle.

6) Normalization single-entry-point rule
- No DB-specific conversion logic in extension methods.
- All normalization goes through the Normalization layer.

7) Grouping rule
- If grouping is used more than once, materialize as `ILookup`/`Dictionary`/`FrozenDictionary`.
- Repeated `GroupBy()` in loops is forbidden.

8) Frozen collections rule (.NET 7+)
- Use frozen only for immutable data with frequent lookups and one-time build cost.
- Do not use frozen for small datasets or frequently changing data.

9) Record vs class rule
- Records only for small immutable DTOs or equality-based comparisons.
- Prefer classes for large datasets to reduce memory/GC pressure.

10) XML docs mandatory
- Any public extension without XML docs is invalid.
- XML docs must answer: Purpose, When to use, When NOT to use, Lifetime/ownership.

11) Benchmark gate
- Any new conversion strategy must be benchmarked (allocations + compare vs fastest path).
- No benchmark → no merge.

12) Deprecation rule
- If a helper is superseded: mark `[Obsolete]`, point to replacement, do not keep both silently.

## Quality gates
Keep build/tests green.
