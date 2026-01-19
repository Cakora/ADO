# linq2db Bulk Copy Plan (AdoAsync)

## Current state
- Bulk import is provider-specific (`src/AdoAsync/Providers/{Provider}/…BulkImportAsync`) using SqlBulkCopy/OracleBulkCopy/PostgreSQL COPY and driven by `BulkImportRequest` + `IDataReader`.
- Validation lives in `AdoAsync.Validation` and the executor routes to provider implementations.

## Goal
- Introduce linq2db-driven bulk copy that works across providers with full control over options, minimal new code, and reuse of existing AdoAsync abstractions. Do not break current paths.

## Design: two approaches, shared core
Folder layout:
- `src/AdoAsync/BulkCopy/LinqToDb/Common/` – option mapping helpers, connection factory.
- `src/AdoAsync/BulkCopy/LinqToDb/DataReader/` – bulk copy using existing `BulkImportRequest` + `IDataReader`.
- `src/AdoAsync/BulkCopy/LinqToDb/Typed/` – bulk copy over `IEnumerable<T>`/`IAsyncEnumerable<T>` for POCOs.

Shared pieces:
- `LinqToDbOptions` (per-provider settings, bulk options, command timeout, keepIdentity, checkConstraints, fireTriggers, notifyAfter, maxDegreeOfParallelism, etc.).
- `LinqToDbConnectionFactory` that wraps existing `DbConnection`/connection string into `DataConnection` (provider names from linq2db: SqlServer, PostgreSQL, Oracle, MySql, SQLite…).
- `BulkCopyOptionsMapper` that translates AdoAsync request/flags into `BulkCopyOptions` (including column mappings, batch size, notifications).
- `ILinqToDbBulkImporter` interface implemented by the two approaches.

Approach A: DataReader bulk (minimal change, reuse existing pipeline)
- Input: `BulkImportRequest` (table, column mappings, batch size, `IDataReader`).
- Flow: map mappings → build `BulkCopyOptions` → call `dataConnection.BulkCopyAsync(options, request.SourceReader)` using `DataConnectionExtensions.BulkCopy`.
- Configuration: per-provider options via `LinqToDbOptions`; uses existing `DbExecutor.BulkImportAsync` (feature-flagged) to route here instead of provider-specific code.
- Use when you already have a reader (current design).

Approach B: Typed bulk (for POCO-based bulk ops)
- Input: `IEnumerable<T>`/`IAsyncEnumerable<T>` + optional column selectors.
- Flow: map property-to-column mapping (attributes or fluent mapping) → `BulkCopyAsync(options, items)`.
- Intended for future consumers wanting typed bulk insert without `IDataReader`.

## Step-by-step implementation
1) Packages
   - Add `linq2db` plus provider packages you need (`linq2db.SqlServer`, `linq2db.PostgreSQL`, `linq2db.Oracle`, `linq2db.MySqlConnector`, `linq2db.SQLite`, etc.).
2) Core setup
   - Create `LinqToDbOptions` POCO and `LinqToDbConnectionFactory` in `Common/`.
   - Add `BulkCopyOptionsMapper` to translate `BulkImportRequest` + options into linq2db `BulkCopyOptions` (batch size, keep identity, check constraints, notify handlers).
3) DataReader approach
   - Add `DataReaderBulkImporter : ILinqToDbBulkImporter` in `DataReader/`.
   - Implement `BulkImportAsync(DbConnection existingConnection, BulkImportRequest request, LinqToDbOptions options, CancellationToken ct)`.
   - Map `request.ColumnMappings` to `BulkCopyOptions.ColumnMappings`.
   - Call `dataConnection.BulkCopyAsync(options, request.SourceReader)`; return `RowsCopied`.
4) Typed approach
   - Add `TypedBulkImporter<T> : ILinqToDbBulkImporter` in `Typed/`.
   - Accept `IEnumerable<T>`/`IAsyncEnumerable<T>` and optional mapping expressions.
   - Use attribute/fluent mapping to align columns.
5) Wiring (non-breaking)
   - Add a feature flag in options (e.g., `UseLinqToDbBulkImport`) and route `DbExecutor.BulkImportAsync` to linq2db importer when enabled; keep existing provider-specific paths as default.
   - For multi-DB deployments, instantiate the factory with provider name based on the current connection string/provider type.
6) Configuration knobs (expose via `LinqToDbOptions`)
   - `BulkCopyType` (Default/ProviderSpecific), `KeepIdentity`, `CheckConstraints`, `MaxDegreeOfParallelism`, `NotifyAfter`, `BatchSize`, `Timeout`, `UseTableLock`, `SkipOnDuplicate`.
   - Optional callbacks: `OnRowsCopied` to surface progress.

## Testing plan
- Unit tests (no DB):
  - `BulkCopyOptionsMapper` maps batch size, identity, constraints, column mappings correctly.
  - Feature flag routes to correct importer.
- Integration tests (per provider: SQL Server, PostgreSQL, Oracle where available):
  - Happy path bulk insert matches row count.
  - Null handling and column mapping order.
  - Identity keep/override, check constraints toggles.
  - Cancellation token honored.
- Use containers where possible; mark Oracle as explicit/skip if not available.

## Sonar/QA
- Run `dotnet build -warnaserror` to include analyzers.
- Run `dotnet test` (integration tests gated by env flags).
- Optional: `dotnet sonarscanner begin …; dotnet build; dotnet sonarscanner end …`.

## Minimal example snippets (for later implementation)
- Factory creation:
  - `var dc = LinqToDbConnectionFactory.Create(connection, providerName, options);`
- DataReader bulk:
  - `await importer.BulkImportAsync(connection, request, options, ct);`
- Typed bulk:
  - `await typedImporter.BulkImportAsync(connection, items, options, ct);`

## Next steps (when ready to code)
- Add packages, create folders, implement common helpers and both importers, add feature flag, then add tests per above. Current code remains untouched until implementation begins.
