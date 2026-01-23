# Conversion Guidelines (AdoAsync)

These rules govern all “post-fetch” conversion helpers (DataReader/DataTable/DataSet/output parameters) into .NET types and collections.

## Mental contract
This library optimizes for memory first, clarity second, speed third.
Streaming is preferred.
Materialization is explicit.
Purpose decides structure.

## Global rules
1. Database access already exists — do not rewrite providers/executor.
2. Conversions happen **after** data is fetched (reader/table/dataset/output parameters).
3. Oracle does not support streaming; prefer buffered paths for Oracle/refcursor.
4. SQL Server / PostgreSQL prefer streaming when possible.
5. Dispose `DataTable`/`DataSet` as soon as mapping is complete.
6. Collection shape is chosen by purpose (memory first, speed second).
7. Extensions must be self-documenting via XML docs (include “When NOT to use”).
8. No provider-specific types in public signatures.
9. Normalization happens once, centrally (no DB-specific conversion in extension methods).

## Design rules (enforced)
### 1) Extension method discovery rule (names must encode behavior)
Extension method names must encode:
- Source type
- Target type
- Behavior (streaming / buffered / grouped)

Goal: when typing `.`, callers can immediately infer memory cost and behavior.

### 2) No default List rule
No method may return `List<T>` by default unless the method name explicitly says it materializes (for example: `ToList...`, `Materialize...`).

### 3) Stream → materialize must be explicit
Streaming extensions must never auto-materialize to `List<T>`/`T[]` internally.
- Streaming extensions return only `IAsyncEnumerable<T>` or `IEnumerable<T>`.
- Materializers live in the AsyncEnumerable category.

### 4) DataSet/DataTable lifetime ownership rule
Any method that accepts `DataSet` or `DataTable` must:
- Document ownership (who disposes what)
- State whether it disposes inputs or not

Default: converters do not dispose; callers dispose immediately after conversion.

### 5) Oracle constraint rule (make it loud)
Oracle paths are always buffered.
No async streaming extensions may be added for Oracle.

### 6) Normalization is single entry point
No extension method may perform DB-specific type conversion directly.
All normalization goes through the Normalization layer.

### 7) Grouping rule (memory)
If grouping is used more than once, it must be materialized as:
- `ILookup<TKey, T>`
- `Dictionary<TKey, T>`
- `FrozenDictionary<TKey, T>`

Repeated `GroupBy()` in loops is forbidden.

### 8) Frozen collection usage rule (.NET 7+)
Use frozen collections only when:
- Data is immutable
- Lookups are frequent
- Build cost is paid once

Do not use frozen collections for small datasets or frequently changing data.

### 9) Record vs class rule
Records are allowed only for:
- Immutable DTOs
- Small result sets
- Equality-based comparison

For large datasets, prefer classes to reduce memory/GC pressure.

### 10) XML documentation is mandatory
Any public extension without XML documentation is invalid.
Each must answer:
1. Purpose
2. When to use
3. When NOT to use
4. Lifetime/ownership (source/result disposal)

### 11) Benchmark justification rule
Any new conversion strategy must be benchmarked against:
- Existing fastest path
- Memory allocations

No benchmark → no merge.

### 12) Removal/deprecation rule
If an existing extension is superseded:
- Mark `[Obsolete]`
- Point to the replacement
- Do not silently keep both long-term

## Collection choice (memory-first)
- `IAsyncEnumerable<T>`: streaming sequential access (no buffering).
- `List<T>`: default materialized collection when size is unknown.
- `T[]`: prefer when count is known (DataTable row count, grouped results).
- `Dictionary<TKey, TValue>`: when repeated key lookups are required.
- `FrozenDictionary<TKey, TValue>`: build once, read many times; only after materialization.

## Default patterns (standard)
- Reader → `IAsyncEnumerable<T>` (SQL Server/PostgreSQL).
- Oracle → `DataTable` → convert → dispose ASAP.
- Multi-group/repeated lookup → `FrozenDictionary<TKey, TValue>` (materialize once, read many).
- One-time filter/projection → keep it streaming (`IEnumerable<T>`/`IAsyncEnumerable<T>`).

## Forbidden patterns (do not do)
- Do not store `DataTable` long-term (convert, then dispose/release).
- No `GroupBy` inside loops (group once after materialization).
- No “List just in case” allocations (materialize only at decision points).
- No DB-specific branching in caller code (normalize centrally).

## Collection selection cheat-sheet
| Purpose | Choose |
|---|---|
| Sequential | `IAsyncEnumerable<T>` |
| One filter | `IEnumerable<T>` |
| Reuse | `List<T>` |
| Fixed size | `T[]` |
| Group once | `ILookup<TKey, T>` |
| Group many | `FrozenDictionary<TKey, T>` |
| Oracle | `DataTable` → convert → dispose |

## Lifetime management (ownership)
Every conversion/materialization method must document:
- Who owns the source (reader/table/dataset/stream)?
- Who owns the result (list/array/dictionary)?
- When the source must be disposed to avoid leaks?
- When the result should be released (drop references)?

## Codex prompt template (use every time)
“Analyze existing extension methods first.
Do not duplicate functionality.
Follow the documented folder structure.
Add XML documentation explaining purpose, when to use, when not to use, and lifetime/ownership.
Optimize for memory first, speed second.
Normalize DB types centrally.
Do not expose DB-specific logic to callers.
Refactor safely: classify existing helpers (keep/replace/remove) before deleting anything.
Keep build/tests green.”

## Final outcome
- `.Dot()` shows intentional methods
- IntelliSense explains why/when/when-not + ownership
- Memory usage is predictable
- Oracle/PostgreSQL/SQL differences disappear behind normalization
- Extensions are reusable, not confusing
