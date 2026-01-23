# AdoAsync Conversion Rules — One Page Cheat Sheet

## Mental contract
Memory first, clarity second, speed third.
Streaming is preferred.
Materialization is explicit.
Purpose decides structure.

## Default patterns
- Reader → `IAsyncEnumerable<T>` (SQL Server/PostgreSQL).
- Oracle/refcursor → `DataTable`/`DataSet` → convert → dispose ASAP.
- One-time filter/projection → keep it streaming (`IEnumerable<T>`/`IAsyncEnumerable<T>`).
- Group/reuse lookups → materialize as `ILookup`/`Dictionary`/`FrozenDictionary`.

## Forbidden patterns
- Store `DataTable` long-term.
- `GroupBy()` inside loops or repeated grouping calls.
- Create `List<T>` “just in case”.
- DB-specific branching in caller code.
- DB-specific conversions outside Normalization.

## Collection selection
| Purpose | Choose |
|---|---|
| Sequential | `IAsyncEnumerable<T>` |
| One filter | `IEnumerable<T>` |
| Reuse | `List<T>` |
| Fixed size | `T[]` |
| Group once | `ILookup<TKey, T>` |
| Group many | `FrozenDictionary<TKey, T>` |
| Oracle | `DataTable` → convert → dispose |

## Naming rules (discoverability)
Extension method names must encode:
- Source type
- Target type
- Behavior (streaming / materialize / grouped)

No default `List<T>`: methods returning `List<T>` must say so (`ToList...`, `Materialize...`).

## Ownership rules
Every conversion/materializer must document:
- Who owns source?
- Who owns result?
- When source must be disposed?
- When result is released?

Default: converters do not dispose inputs; caller disposes immediately after conversion.

## Oracle rule (loud)
Oracle is always buffered. No async streaming extensions for Oracle.

## Bench/Deprecation gates
- New conversion strategy: benchmark allocations + compare to fastest path.
- Superseded helpers: mark `[Obsolete]` and point to replacement.

