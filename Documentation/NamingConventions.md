# Naming Conventions — Conversion Extensions

Goal: when typing `.`, names should reveal source → target → behavior (memory + lifecycle).

## General rules
- Prefer verb-first names (`Stream...`, `To...`, `Map...`, `ToFrozen...`).
- Avoid generic names like `Convert`, `AsList`, `Transform` without source/target qualifiers.
- If a method materializes, the name must explicitly indicate materialization (`ToList...`, `ToArray...`, `ToFrozenDictionary...`).
- If a method is streaming, the name must explicitly indicate streaming (`Stream...`).

## Source type in the name
Include the source in either:
- the method name, or
- the containing type name (extension class name)

Examples:
- `DbDataReaderExtensions.StreamAsync<T>(...)` (source is implied by extension class)
- `AsyncEnumerableMaterializerExtensions.ToFrozenDictionaryAsync(...)` (source is implied by extension class)

## Behavior keywords
Use these keywords consistently:
- `Stream` → streaming, no buffering
- `ToList` / `ToArray` → materialization
- `ToLookup` / `ToFrozenDictionary` → grouped/indexed materialization
- `Map` → mapping (caller supplies mapper); may be streaming or buffered depending on source

## Async rules
- Async materializers: `...Async` suffix (e.g., `ToListAsync`, `ToFrozenDictionaryAsync`).
- Streaming enumerables: prefer returning `IAsyncEnumerable<T>` and name as `Stream...` (even if method itself is not `async`).

## Duplication avoidance
Before adding a new extension:
- Check for an existing method that already covers the scenario.
- If behavior differs (stream vs buffered), the name must make that difference obvious.

