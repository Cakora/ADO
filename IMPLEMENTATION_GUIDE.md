# UPDATED STEP-BY-STEP IMPLEMENTATION GUIDE (CODEX-SAFE)

## Quick Navigation
- Previous discussion summary: **CONSOLIDATED REQUIREMENTS (FROM PREVIOUS DISCUSSION)**
- Global rules: **GLOBAL RULES (APPLY TO ALL STEPS)**
- Architecture: **STEP 0 — ARCHITECTURE PROFILE**
- Usage: **STEP 1 — DEFINE HOW THE LIBRARY IS USED**
- Implementation checklist: **DETAILED STEP-BY-STEP BUILD CHECKLIST (DO THIS IN ORDER)**

## CONSOLIDATED REQUIREMENTS (FROM PREVIOUS DISCUSSION)

These are mandatory, always-on constraints to apply whenever new code is created:
- .NET 8, async-first and async-only public API
- Provider-agnostic, generic naming in facade/resilience/validation layers
- ADO.NET execution with low-memory / low-GC patterns (streaming readers, avoid unnecessary allocations)
- Retry is central but controllable: retry behavior is explicit, optional (`EnableRetry`), and implemented with Polly when enabled (Resilience layer only)
- Validation implemented with FluentValidation (Validation layer only)
- Quality gates every time: .NET analyzers, SonarQube analysis, `dotnet pack`/NuGet validation, warnings-as-errors
- Shared provider-agnostic code lives in `Database.Common` (no execution logic there)

## GLOBAL RULES (APPLY TO ALL STEPS)

### Technology & Scope
- Target framework: .NET 8
- Async-only library (no sync entrypoints in public API; if a sync wrapper is ever required, it must live outside core and must wrap async)
- Type: Git-based Class Library
- Data access: ADO.NET only
- Databases supported:
  - SQL Server
  - PostgreSQL
  - Oracle
- Use modern .NET 8 features where beneficial:
  - `ValueTask` / `IAsyncEnumerable<T>`
  - `ReadOnlySpan<T>` / `ReadOnlyMemory<T>` for parameter/value handling where safe
  - Nullable reference types and .NET analyzers
  - `DbDataSource` when available (provider supports it)

### .NET 8 / C# feature guidance (performance-focused)
- Prefer stable language/runtime features (no preview features unless explicitly approved)
- Use modern primitives to reduce allocations and GC pressure:
  - Generic collections (`List<T>`, `Dictionary<TKey, TValue>`) over non-generic collections
  - `System.Collections.Frozen` (`FrozenDictionary`, `FrozenSet`) for read-mostly lookup tables/config (build once, then reuse)
  - `Array` / `ReadOnlyMemory<T>` for bulk data passing instead of allocating many small objects
  - `Span<T>` / `ReadOnlySpan<T>` for temporary, stack-only parsing/formatting work (no heap allocation)
  - `ArrayPool<T>` / `MemoryPool<T>` only when explicitly designed and documented (avoid hidden pooling side effects)
  - `ValueTask` only for hot paths where it measurably reduces allocations; otherwise prefer `Task` for simplicity
- Use modern C# (C# 12 on .NET 8) features when they improve readability/performance:
  - Switch expressions + pattern matching (avoid long `if/else` ladders when it makes logic clearer)
  - `record` / `record struct` for immutable option/result/contract models (prefer `readonly record struct` for small value-like types to reduce allocations)
  - Collection expressions where they reduce noise without hiding logic

### Async-first design
- Async-first and async-only: public APIs are asynchronous (`async`/`await`, `Task`/`ValueTask`, `IAsyncEnumerable<T>`)
- If any sync API exists, it MUST be a thin wrapper around async (no duplicate logic)
- Explicit code over implicit behavior
- Readability and debuggability are mandatory
- Always prefer .NET 8 features and best practices (use “latest” C# language features available for .NET 8 when they improve clarity/performance)

### Global Must NOT (Critical for Codex)
- No ORM (Entity Framework, Dapper, etc.)
- No DI containers
- No static global helpers
- No reflection-based magic
- No provider-specific logic outside providers
- No hidden retries
- No hidden transactions
- No hidden logging
- No global mutable state

### Error handling rules (custom + generic + middleware-ready)
- Do NOT use predefined/provider exception types as the public error contract
- Expose a custom, provider-agnostic error model with generic names (example names: `DbError`, `DbErrorType`, `DbErrorCode`)
- Providers MUST translate native exceptions into the custom error model; raw DB exceptions MUST NOT escape
- Error model must be middleware-friendly (serializable and stable across versions)
- Multi-language support: error contract must support returning localized messages (e.g., `MessageKey` + `MessageParameters` + optional `LocalizedMessage`)

### Coding principles & standards (mandatory)
- Follow clean code principles: small methods, clear naming, explicit control flow, no hidden side effects
- Follow consistent formatting and style across the codebase
- Use descriptive, generic names for public APIs (no database-specific naming in the facade layer)
- Prefer allocation-aware patterns for high performance (avoid unnecessary `DataTable`/boxing/extra copies unless required by the result contract)
- Prefer streaming reads and forward-only access patterns in ADO.NET to reduce memory and GC pressure (e.g., `DbDataReader` with sequential access)
- Avoid per-call allocations in hot paths (minimize closures, avoid LINQ in core execution paths, reuse buffers only when safe and non-global)
- Use `ConfigureAwait(false)` in library code
- Always accept `CancellationToken` for async operations

### Debuggability & simplicity (mandatory)
- Keep the implementation easy to debug and easy to follow:
  - Prefer explicit, step-by-step code over clever abstractions
  - Avoid “magic” patterns (no reflection-driven mapping, no expression-tree codegen, no hidden behaviors)
  - Keep control flow linear and predictable (one responsibility per method; minimal nesting)
  - Prefer readable, provider-agnostic primitives in core (`DbConnection`, `DbCommand`, `DbDataReader`)
- Favor observability via returned diagnostics (`DbResult`) rather than logging or hidden tracing.

### Commenting standard (mandatory)
- Public APIs: XML documentation comments are required (IntelliSense/tooltips).
- Internal code: add clear, line-by-line comments in critical areas where mistakes are expensive:
  - Provider type mapping (`DbDataType` → provider parameter types)
  - Error translation (native exception → `DbError`)
  - Transaction handle lifecycle (begin/commit/rollback/Dispose)
  - Multi-result reading (`NextResultAsync`) and Oracle cursor handling
- Comments must explain “why/what” each line is doing (not restating obvious syntax) and must stay in sync with the code.

### Object lifetime & GC pressure (mandatory for large applications)
- Minimize object churn in hot paths: avoid creating many short-lived objects per row/per call (especially dictionaries, DataTables, reflection-based mappers)
- Prefer a single executor per logical operation/request and reuse it sequentially for multiple commands (do not create a new executor for every single query)
- `DbExecutor` owns the connection lifecycle:
  - Create lazily (no connection opened in constructor)
  - Open just-in-time for the first execution
  - Reuse the same open connection for the executor lifetime (and for the entire transaction if started)
  - Close/dispose automatically on `DisposeAsync`/`Dispose` (no manual `Dispose()` calls by consumers)
- Consumer usage must rely on scope-based disposal (`await using var db = ...`) so the connection is always closed even on exceptions
- Do NOT add finalizers; rely on deterministic disposal (`IAsyncDisposable`) to avoid GC finalization overhead
- Connection pooling is controlled by the provider/connection string only; the library must not implement hidden pooling or global caches
- If pooling is needed for large buffers, it must be explicit and opt-in (e.g., an injected `ArrayPool<T>`/`MemoryPool<T>` via options), not global and not hidden

### Naming (generic + consistent)
- Public API names must be generic and provider-agnostic: `DbExecutor`, `DbOptions`, `DbResult`, `DbError`, `DbParameter`
- Avoid leaking provider-specific terms into the facade/resilience/validation layers (provider-specific naming stays inside Providers)

### Quality gates (always enforced)
- Enable .NET analyzers and fix violations (build should fail on new violations)
- Run SonarQube analysis and address findings (no suppressions without explicit justification)
- Run NuGet package validation / `dotnet pack` checks before release
- Treat warnings as errors for library projects

### Public API surface (generic + easy-to-use)
- Provide a small set of “shapes” that cover most needs:
  - `ExecuteAsync` (non-query)
  - `ExecuteScalarAsync<T>` (scalar)
  - `QueryAsync<T>` (streaming rows with mapper)
  - Materialization APIs are optional and explicit:
    - `QueryTablesAsync` / optional `QueryDataSetAsync` (high allocation; use only when needed)
    - Domain mapping remains the primary path for large apps (`QueryAsync<T>` + typed mapper)
- All execution must accept:
  - `CommandDefinition` (SQL/proc + parameters + timeout + command type)
  - `CancellationToken`
 - Usage MUST be the same for all providers (SQL Server/PostgreSQL/Oracle): only `DbOptions.DatabaseType` and connection string change

### Stored procedures (explicit)
- Support both `CommandType.Text` and `CommandType.StoredProcedure` via `CommandDefinition`
- Output parameters MUST require `Size` and must be returned as typed output values in the result contract
- Provider layer owns procedure-specific details (e.g., Oracle cursor outputs)

### Stored procedure rules (command type + output parameters + provider ownership)
- Command type MUST be explicit:
  - Stored procedures use `CommandType.StoredProcedure` and `CommandText` is the procedure name (do not require `EXEC ...` text)
  - Raw SQL uses `CommandType.Text` (no auto-detection)
- Output parameters:
  - Must set `Direction = Output` or `InputOutput`
  - Must specify `Size` (mandatory) and must be validated (FluentValidation) before execution
  - Must be returned via the library’s output-parameter result contract (no mutation-based “out” patterns)
- Provider ownership:
  - Providers implement parameter binding and exception translation and own all database-specific procedure behavior
  - Oracle provider owns cursor/REF CURSOR handling and cursor tracking; cursor-related errors are not retried
  - Facade/Resilience/Validation layers must remain provider-agnostic (no provider-specific branching)

### SQL injection & dynamic SQL rules (security-critical)
- Values MUST always be parameterized (no concatenation of user input into SQL)
- The library MUST NOT encourage interpolated SQL; parameters are the only safe input channel for values
- Identifiers (table/column/order-by) MUST NOT accept user input directly; dynamic identifiers must use a whitelist mapping in consumer code
- Validation MUST reject invalid parameter definitions (missing name/type/value, output parameter without `Size`)

Detailed rules (mandatory):
- Separate **values** vs **identifiers**:
  - Values (safe via parameters): ids, names, amounts, dates, search text
  - Identifiers (NOT safely parameterizable): table/column/schema names, `ORDER BY` columns, sort direction
- Parameterization rules (values):
  - All untrusted values MUST be passed as parameters (`@p0`, `@id`, etc.)
  - No string concatenation or interpolation of user values into SQL text
- Identifier rules (dynamic SQL):
  - Identifiers MUST come from an allow-list/whitelist mapping in consumer code
  - Raw user input MUST NEVER be placed into SQL as an identifier
  - If dynamic identifiers are required, map user tokens → constant safe fragments (e.g., `"name"` → `"u.Name"`)
- IN-list and paging notes:
  - For `IN (...)` lists, generate one parameter per value (do not inject `1,2,3` text)
  - For `ORDER BY`, whitelist both column and direction (`ASC`/`DESC`)
- Stored procedure notes:
  - Procedure name must not come from user input; if it must, it MUST be whitelisted
  - Procedure inputs/outputs still use parameters only

### Result strategy (low memory by default)
- Default is streaming (`QueryAsync<T>`) to avoid buffering large result sets in memory
- Materialized results (DataTable/DataSet) are allowed only as explicit APIs and must be documented as allocation-heavy
- Document provider type differences and normalization boundaries (see `docs/type-handling.md`).
- Multiple result sets (multi-data support)
  - Decision: this library MUST support multi-result queries/procedures
  - Must support multiple result sets from a single command (when the provider supports it natively)
  - Streaming approach: sequentially read each result set (`NextResultAsync`) and expose it explicitly (do not buffer unless requested)
  - Materialized approach (explicit): return `List<DataTable>` (and optionally `DataSet` if required by consumers)
  - Provider notes:
    - SQL Server: native multiple result sets
    - PostgreSQL / Oracle: provider-specific handling (sequential reads or cursor-based patterns); must remain provider-owned

Detailed discussion (mandatory):
- Streaming default (recommended for large apps):
  - Primary query API returns `IAsyncEnumerable<T>` and reads rows via `DbDataReader`
  - Results are processed row-by-row without buffering the full result set
  - Prefer forward-only + `SequentialAccess` where appropriate to reduce memory and GC pressure
- Materialized results are explicit (convenience, allocation-heavy):
  - Provide explicit APIs such as `QueryTablesAsync` and optional `QueryDataSetAsync`
  - These APIs buffer the full result set into memory and are inherently high-allocation (DataRow/DataColumn/object graphs)
  - Use only for small result sets or where consumers require `DataTable`/`DataSet` (legacy/UI/data-binding)
- Multi-result handling:
  - Streaming: consume result sets sequentially (`NextResultAsync`) and expose an explicit API shape so callers opt in
  - Materialized: return `List<DataTable>` / `DataSet` as the explicit multi-result materialization

### Transactions (ergonomics without hidden behavior)
- Prefer explicit transactions on the executor/session (no ambient `TransactionScope` in core)
- Reduce boilerplate with one of these explicit patterns:
  - Transaction handle (`await using var tx = await db.BeginTransactionAsync(...)`; rollback-on-dispose unless committed)
  - Helper API (`ExecuteInTransactionAsync(Func<CancellationToken, Task> ...)`) that clearly starts/commits/rolls back

Future extensibility (optional, not in core):
- Primary design is Approach A (explicit `DbTransaction` with rollback-on-dispose handle).
- The architecture should allow adding Approach B later as an optional add-on package/module (ambient `TransactionScope`) without changing core behavior:
  - Keep core transaction APIs explicit and provider-agnostic.
  - If ambient support is added, it must be opt-in, documented as advanced, and must not become the default.
  - Ambient transactions must never bypass the library’s custom error contract, validation, or diagnostics rules.

### IntelliSense documentation (developer experience)
- All public types/methods must have XML documentation comments so IDE tooltips show usage and rules
- Keep docs concrete: include examples for scalar, non-query, streaming query, stored procedure, output parameters, transactions

Detailed requirements (mandatory):
- Enable XML docs generation for library projects (tooltips + packaged docs).
- Public XML docs MUST clearly state:
  - Memory behavior (streaming vs materialized; DataTable/DataSet are allocation-heavy)
  - Security rules (parameterization for values; whitelist mapping for identifiers)
  - Transaction behavior (explicit; rollback-on-dispose handle; no ambient transactions in core)
  - Retry behavior (optional via `EnableRetry`; Polly-backed when enabled; not inside explicit transactions)
  - Cancellation behavior (all async APIs accept `CancellationToken`)
- Examples MUST be short, copyable, and safe-by-default.

Examples to include in XML docs (templates):
- Non-query:
  - `await db.ExecuteAsync("UPDATE ... WHERE Id=@id", new[] { new DbParameter("id", DbDataType.Int32, 1) }, ct);`
- Scalar:
  - `var count = await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM T WHERE X=@x", new[] { new DbParameter("x", DbDataType.String, v) }, ct);`
- Streaming query (recommended for large results):
  - `await foreach (var row in db.QueryAsync("SELECT ...", p, r => new Row(r.GetInt32(0), r.GetString(1)), ct)) { ... }`
- Stored procedure + output parameters:
  - `var result = await db.ExecuteAsync(new CommandDefinition("ProcName", CommandType.StoredProcedure, parameters: ...), ct);`
- Transaction handle (Approach A):
  - `await using var tx = await db.BeginTransactionAsync(ct); ... await tx.CommitAsync(ct);`

---

## STEP 0 — ARCHITECTURE PROFILE (MANDATORY, READ FIRST)

This step defines the overall architecture. ALL later steps MUST conform to this architecture.

### ARCHITECTURE STYLE
The library follows a strict layered architecture:

Facade + Provider + Resilience + Infrastructure

There are NO alternative patterns allowed.

### ARCHITECTURE LAYERS (TOP TO BOTTOM)

#### Layer 1 — Consumer Layer
- Application code
- EF Core (if used)
- Calls Database.Core explicitly

#### Layer 2 — Facade Layer
- DbExecutor
- Entry point for all operations
- Orchestrates execution only

#### Layer 3 — Resilience Layer
- RetryPolicy
- ExecutionTimer
- Handles retry, timeout, diagnostics

#### Layer 4 — Provider Layer
- SqlServerProvider
- PostgreSqlProvider
- OracleProvider
- Handles database-specific behavior

#### Layer 5 — Infrastructure Layer
- ADO.NET
- DbConnection
- DbCommand
- DbTransaction

### ARCHITECTURE FLOW (EXPLICIT)
Consumer → DbExecutor (Facade / Orchestrator ONLY) → RetryPolicy + ExecutionTimer → Database Provider (SQL / PostgreSQL / Oracle) → ADO.NET

### ARCHITECTURE RULES (ABSOLUTE)
- Dependency direction is ONE-WAY only (top → down)

- DbExecutor MAY depend on:
  - Providers
  - Resilience
  - Transactions
  - Validation
  - Configuration

- Providers MAY depend ONLY on:
  - ADO.NET
  - Type mapping
  - Error translation

- Providers MUST NOT depend on:
  - DbExecutor
  - RetryPolicy
  - TransactionManager
  - Validation
  - Other providers

- Resilience MUST NOT depend on providers
- Validation MUST NOT depend on providers
- Extensions MUST NOT contain core logic

- Codex MUST NOT:
  - Introduce repository pattern
  - Introduce unit-of-work abstraction
  - Introduce service layer
  - Introduce base classes with shared logic
  - Introduce circular dependencies

---

## STEP 1 — DEFINE HOW THE LIBRARY IS USED (ABSOLUTELY FIRST)

### Goal
Lock how developers will use the library, so internal design follows usage.

### Object Creation & Lifetime
- Library is used via new or factory
- No DI container
- Object MUST be auto-disposed
- Developer MUST NOT call Dispose manually

### Executor Requirements
- Implements IAsyncDisposable
- MUST NOT require consumer to call `Dispose()` manually; usage should be `await using` / `await using var`
- If a synchronous wrapper is ever required, it must live outside core and must wrap async (no duplicated execution logic)

### Usage Shape (conceptual)
Using var db = DbExecutor.Create(options);

### Thread Safety
- DbExecutor is NOT thread-safe
- One executor = one logical operation
- Sequential reuse allowed
- Concurrent use forbidden

### Connection Lifecycle
- Created lazily
- Opened just before execution
- Closed on dispose
- Same connection reused during transaction
- Retry MUST NOT recreate executor
- Pooling controlled only via connection string

---

## STEP 2 — DEFINE ALL CONFIGURATION (NO LOGIC)

### DbOptions MUST include
- DatabaseType
- ConnectionString
- CommandTimeoutSeconds
- RetryCount
- RetryDelayMilliseconds
- EnableRetry
- EnableValidation
- EnableDiagnostics

### Rules
- No execution logic
- No hidden defaults
- No environment reads

Explicit defaults (documented and stable; not “hidden defaults”):
- `EnableDiagnostics = false`
- `EnableValidation = true`
- `EnableRetry = false`
- `RetryCount = 3`
- `RetryDelayMilliseconds = 200`

---

## STEP 3 — DEFINE PARAMETER MODEL

Each parameter MUST define:
- Name
- Direction
- DbDataType (DB-agnostic enum)
- Value
- Size (mandatory for output)

### Size considerations (performance + correctness)
- For variable-length **input** parameters (strings/binary), callers SHOULD set `Size` to the known maximum (e.g., 400) instead of leaving it unbounded.
  - Reduces oversized allocations/buffers and avoids implicit conversions in some providers.
- For variable-length **output** parameters, `Size` is **mandatory** so the provider allocates the correct output buffer.

### Security
- ALL user input MUST be parameterized
- String interpolation forbidden

### DbDataType (complete, cross-provider) + mapping precautions

This library MUST define a DB-agnostic `DbDataType` enum that covers all common types used in large applications, including JSON, XML, and binary. Provider projects own the exact mapping to provider-specific parameter types.

Rules:
- Caller supplies `DbDataType` (never “guess” types from `object` at runtime).
- Provider must map `DbDataType` → provider parameter type (`DbType` and/or provider-specific type) deterministically.
- For variable-length types, `Size` must be supported and used when appropriate (mandatory for output params).
- For decimals, `Precision`/`Scale` must be supported and used when appropriate.
- For date/time, conversion must never rely on string formatting; always send typed parameters.

Recommended `DbDataType` set (do not omit these):
- **Strings**
  - `String` (Unicode, variable length)
  - `AnsiString` (non-Unicode, variable length)
  - `StringFixed` / `AnsiStringFixed` (CHAR/NCHAR equivalents)
  - `Clob` / `NClob` (large text)
- **Numbers**
  - `Int16`, `Int32`, `Int64`
  - `Byte`, `SByte`
  - `UInt16`, `UInt32`, `UInt64` (validate range carefully; many DBs do not natively support unsigned)
  - `Decimal` (requires precision/scale support)
  - `Double`, `Single`
  - `Currency` (maps to money-like types where available; otherwise decimal)
- **Booleans**
  - `Boolean`
- **GUID/UUID**
  - `Guid`
- **Binary**
  - `Binary` (byte array)
  - `Blob` (large binary)
- **Date/Time**
  - `Date` (date only)
  - `Time` (time only)
  - `DateTime` (date+time; legacy)
  - `DateTime2` (preferred “date+time” with precision where supported)
  - `DateTimeOffset` (date+time+offset)
  - `Timestamp` (rowversion/byte timestamp semantics where applicable; do NOT confuse with date-time)
  - `Interval` (duration; `TimeSpan`-like)
- **Structured / special**
  - `Json` (JSON payload)
  - `Xml` (XML payload)

Provider mapping cautions (must be documented and tested):
- **SQL Server**
  - Unicode strings: prefer `nvarchar` (`DbDataType.String`).
  - JSON: no native JSON type; store as `nvarchar(max)` and validate JSON at app/layer if needed (`DbDataType.Json` → string).
  - XML: native `xml` type exists (`DbDataType.Xml`).
  - `DateTime2`/`DateTimeOffset` are preferred over legacy `datetime`.
  - `Timestamp` is `rowversion`/`timestamp` (binary), not a date-time.
- **PostgreSQL**
  - JSON: prefer `jsonb` when possible (`DbDataType.Json` → jsonb), otherwise `json`.
  - XML: `xml` type exists (`DbDataType.Xml`).
  - GUID: native `uuid` (`DbDataType.Guid`).
  - `DateTimeOffset`: maps to `timestamp with time zone`; clarify semantics (Postgres stores UTC-normalized timestamp; it does not preserve the original offset).
  - `Interval`: native `interval` (use for durations; avoid storing as strings).
- **Oracle**
  - Boolean: traditionally not a SQL column type (pre-23c); map `DbDataType.Boolean` to `NUMBER(1)` (0/1) or `CHAR(1)` consistently inside the Oracle provider.
  - GUID: commonly stored as `RAW(16)` (preferred for space/perf) or `CHAR(36)`; choose one mapping and be consistent (`DbDataType.Guid`).
  - JSON: storage is commonly `CLOB`/`VARCHAR2` with JSON constraints; newer Oracle versions may support a JSON data type—provider must own and document the mapping for the targeted Oracle versions.
  - XML: commonly `XMLTYPE`; support depends on provider/version—document the supported mapping strategy (`DbDataType.Xml`).
  - Date/time: Oracle `DATE` includes time (seconds precision); `TIMESTAMP`/`TIMESTAMP WITH TIME ZONE` have different semantics—Oracle provider must map `Date`, `DateTime2`, `DateTimeOffset` explicitly.
  - `Interval`: use `INTERVAL DAY TO SECOND` mapping where supported; otherwise define a documented fallback (e.g., store ticks as number) but keep it provider-owned and explicit.

Validation requirements (must be enforced via FluentValidation when enabled):
- Output parameters require `Size` (strings/binary) and may require `Precision`/`Scale` (decimal).
- Unsigned integer values must be rejected or converted safely when the provider cannot represent them natively.
- `Timestamp` must be validated so it is not used as a date-time type.

---

## STEP 4 — DEFINE RESULT MODEL

DbResult MUST contain:
- Success
- Tables (List of DataTable)
- ScalarValue
- OutputParameters
- ExecutionDuration
- RetryCount
- Error

Memory note (important):
- Materializing `DataTable` is inherently allocation-heavy; the library must also expose streaming APIs for large result sets, and only materialize tables when the caller explicitly asks for materialized results.

### Multiple Results
- SQL Server: native
- PostgreSQL / Oracle: sequential or cursors
- Always return List of DataTable

---

## STEP 5 — ERROR & ERROR CODE CONTRACT

### Standard Error Model
- ErrorType
- ErrorCode
- Message
- IsTransient
- ProviderDetails

### Error Categories
- Timeout
- Deadlock
- ConnectionFailure
- ResourceLimit
- ValidationError
- SyntaxError
- Unknown

Providers MUST translate native exceptions. Raw DB exceptions MUST NOT escape.

---

## STEP 6 — FOLDER STRUCTURE (EXPLICIT, COPYABLE, FINAL)

Database.Core

- Abstractions
  - IDbExecutor
  - IDbProvider
  - ITransactionManager
  - Interfaces ONLY

- Configuration
  - DbOptions

- Providers
  - SqlServerProvider
  - PostgreSqlProvider
  - OracleProvider
  - Database-specific logic ONLY

- Execution
  - DbExecutor
  - DbCommandFactory

- Parameters
  - DbParameter
  - DbParameterCollection

- Results
  - DbResult
  - ScalarResult

- Resilience
  - RetryPolicy
  - ExecutionTimer

- Transactions
  - TransactionManager

- Validation
  - DbValidator

- Extensions
  - Helper extensions only
  - NO execution logic

### Rules
- No new folders allowed
- No logic in Abstractions
- No shared logic between providers

### Common library (mandatory to avoid duplication)

Create a separate project for shared, provider-agnostic code:

`Database.Common`

Rules:
- Contains ONLY shared contracts and utilities (no execution logic)
- Must be provider-agnostic (no SQL Server / PostgreSQL / Oracle dependencies or types)
- Must not introduce global mutable state or hidden behavior
- May contain (examples):
  - Shared enums and error/result contracts (`DbError`, `ErrorType`, error codes)
  - Shared guard/argument validation helpers (pure, no I/O)
  - Small, allocation-aware helper types used across layers
- Must NOT contain:
  - Retries, transactions, command execution, connection management
  - Logging, environment reads, configuration loading

---

## STEP 7 — TRANSACTION DESIGN

- Explicit begin / commit / rollback
- Async-first
- Sync wraps async
- Rollback on ANY failure
- Same connection per transaction

---

## STEP 8 — RESILIENCE (RETRY + TIMEOUT)

Retry ONLY on:
- Timeout
- Deadlock
- Transient connection failure

Retry NEVER on:
- Validation errors
- Syntax errors

CancellationToken mandatory:
- Cancellation cancels command
- Rolls back transaction
- Stops retries

### Polly requirement (retry implementation)
- Retry is optional and controlled by `DbOptions.EnableRetry` (recommended default: `false`)
- When retry is enabled, use **Polly** as the only retry engine (no custom retry loops in core)
- Retry policy MUST be explicitly constructed from `DbOptions` (no hidden defaults)
- Only the Resilience layer may reference Polly
- Providers MUST NOT reference Polly
- Retry MUST NOT create hidden transactions
- Retry MUST NOT log; diagnostics must flow via `DbResult`

---

## STEP 9 — ORACLE-SPECIFIC RULES

- Cursor tracking mandatory
- ORA-01000 → ResourceLimit
- ORA-12170 → Timeout
- Cursor errors NOT retried

---

## STEP 10 — VALIDATION

Checks:
- Null options
- Invalid timeout
- Missing parameters
- Output parameters without size

### FluentValidation requirement (validation implementation)
- Use **FluentValidation** for validation rules (no ad-hoc `if` chains spread across layers)
- Validation layer may reference FluentValidation; other layers must not
- Validation MUST be explicitly invoked based on `DbOptions.EnableValidation`
- Validation errors MUST map to `ErrorType = ValidationError` and MUST NOT be retried

---

## STEP 11 — LOGGING & DIAGNOSTICS

- No logging in core
- Diagnostics returned via DbResult
- Consumer controls logging

---

## STEP 12 — TESTING (SEPARATE PROJECT)

Separate Git project. References Database.Core.

Tests:
- Positive
- Negative
- Timeout
- Retry
- Transaction rollback
- Oracle cursor exhaustion

---

## STEP 13 — ANALYZERS, SECURITY & QUALITY

- Enable nullable
- Enable .NET analyzers
- Treat warnings as errors
- NuGet vulnerability scan
- SonarQube analysis

Build MUST fail on violations.

---

## STEP 14 — GIT & CI PIPELINE

Git repository:
- main / dev branches

CI runs:
- Build
- Tests
- Analyzers
- SonarQube
- NuGet validation

---

## STEP 15 — IMPLEMENTATION ORDER

1. Architecture & folder structure
2. Usage & options
3. Data models
4. Abstractions
5. Providers
6. Executor
7. Resilience
8. Transactions
9. Validation
10. Tests
11. Quality gates
12. NuGet packaging
13. Bulk import (optional extension)

---

## DETAILED STEP-BY-STEP BUILD CHECKLIST (DO THIS IN ORDER)

This is a copyable, end-to-end checklist to implement the library with the features discussed (async-only, streaming-first, provider-agnostic, Polly retry, FluentValidation, custom error contract, low-GC patterns, analyzers/Sonar/NuGet pack).

### 1) Repo + solution setup
- Create a `.NET 8` solution and projects (Git-based class library).
- Enable `nullable` and .NET analyzers.
- Enforce warnings-as-errors for library projects.
- Add a `Directory.Build.props` / `Directory.Build.targets` (preferred) to keep rules consistent across all projects.

Acceptance:
- `dotnet build -c Release` succeeds with warnings-as-errors enabled.

### 2) Project layout (folders + assemblies)
- Create these projects (assemblies), aligned to layers:
  - `Database.Common` (shared contracts/utilities; no execution logic)
  - `Database.Core` (facade + orchestration + resilience + validation + transactions; references `Database.Common`)
  - `Database.Providers.SqlServer` (provider only; references `Database.Common`)
  - `Database.Providers.PostgreSql` (provider only; references `Database.Common`)
  - `Database.Providers.Oracle` (provider only; references `Database.Common`)
  - `Database.Tests` (separate project; references `Database.Core` and providers as needed)

Acceptance:
- No provider project references `Database.Core`.
- `Database.Common` contains no ADO.NET command execution.

### 3) Define configuration contracts (NO logic)
- Implement `DbOptions` with all required fields:
  - `DatabaseType`, `ConnectionString`, `CommandTimeoutSeconds`
  - `EnableRetry`, `RetryCount`, `RetryDelayMilliseconds`
  - `EnableValidation`, `EnableDiagnostics`
- No defaults unless explicitly defined and documented.
- No environment reads.

Acceptance:
- Options are plain data (immutable `record`/`record struct` where appropriate).

### 4) Define custom error contract (middleware + multi-language)
- In `Database.Common`, define:
  - `DbError` (serializable, provider-agnostic)
  - `DbErrorType` (Timeout, Deadlock, ConnectionFailure, ResourceLimit, ValidationError, SyntaxError, Unknown)
  - `DbErrorCode` (generic codes; stable over time)
  - Localization fields: `MessageKey`, `MessageParameters`, optional `LocalizedMessage`
- Providers translate native exceptions into `DbError`.
- Raw provider exceptions MUST NOT escape as the public contract when using result envelopes.

Acceptance:
- Any failure yields a consistent, serializable error shape suitable for middleware.

### 5) Define parameter model (DB-agnostic + injection-safe)
- In `Database.Common`, define:
  - `DbDataType` enum (DB-agnostic types)
  - `DbParameter` model with `Name`, `Direction`, `DbDataType`, `Value`, and mandatory `Size` for output
  - `DbParameterCollection` (allocation-aware; avoid per-call heavy allocations)
- Security rules:
  - Values MUST be parameterized.
  - Identifiers (table/column/order-by) must be whitelisted in consumer code (not parameterized).

Acceptance:
- Validation catches output parameters missing `Size`.

### 6) Define command model (text/proc + behavior)
- In `Database.Common` (or `Database.Core.Configuration` if you prefer), define `CommandDefinition`:
  - `CommandText` / proc name
  - `CommandType` (`Text` / `StoredProcedure`)
  - Parameters
  - Timeout override (optional)
  - Command behavior for readers (default streaming-friendly)

Acceptance:
- Same command definition works for text SQL and stored procedures.

### 7) Define result model (streaming-first + explicit materialization)
- In `Database.Common`, define:
  - `DbResult<T>` (or `DbResult` + separate scalar/table results)
  - Required fields: `Success`, `ScalarValue` (if applicable), `Tables` (materialized), `OutputParameters`, `ExecutionDuration`, `RetryCount`, `Error`
- Streaming rule:
  - Primary query API returns `IAsyncEnumerable<T>` (streaming).
  - Materialized APIs (`List<DataTable>` / optional `DataSet`) are explicit and documented as allocation-heavy.

Acceptance:
- “Materialize” is never the default; streaming is the default.

### 8) Abstractions (interfaces only)
- In `Database.Core/Abstractions`, define:
  - `IDbExecutor` (facade surface)
  - `IDbProvider` (provider capabilities: create connection/command, apply parameters, translate errors)
  - `ITransactionManager` (explicit begin/commit/rollback contracts)

Acceptance:
- No logic inside abstractions.

### 9) Validation (FluentValidation only)
- Implement validators in `Database.Core/Validation` using FluentValidation:
  - `DbOptionsValidator`
  - `CommandDefinitionValidator`
  - `DbParameterValidator`
- Validation is invoked only when `DbOptions.EnableValidation = true`.
- Validation errors map to `DbErrorType.ValidationError` and are never retried.

Acceptance:
- Invalid parameter/output size fails fast before hitting ADO.NET.

### 10) Resilience (Polly only)
- Implement `RetryPolicy` in `Database.Core/Resilience` using Polly:
  - Policy built explicitly from `DbOptions` (`RetryCount`, `RetryDelayMilliseconds`, `EnableRetry`)
  - Retry ONLY for transient categories (Timeout/Deadlock/ConnectionFailure as defined)
- No logging; diagnostics returned via `DbResult`.
- No hidden transactions: define explicit behavior for “retry inside user transaction” (recommended: do not retry when a user transaction is active).

Acceptance:
- Retries happen only for allowed error types and stop on cancellation.

### 11) Transactions (explicit + ergonomic)
- Implement explicit begin/commit/rollback:
  - Same connection reused for the entire transaction.
  - Rollback on any failure.
- Provide an ergonomic pattern that reduces boilerplate without hiding behavior:
  - Transaction handle with rollback-on-dispose unless committed, OR
  - `ExecuteInTransactionAsync(Func<CancellationToken, Task>)`

Acceptance:
- No `TransactionScope` usage in core.

### 12) Providers (SQL Server / PostgreSQL / Oracle)
- Each provider project implements `IDbProvider` and contains ONLY provider-specific logic:
  - Creating `DbConnection`/`DbCommand`
  - Mapping `DbDataType` to provider parameter types
  - Exception translation into `DbError` (provider details captured, but contract generic)
- Oracle rules:
  - Cursor tracking mandatory
  - ORA-01000 → ResourceLimit
  - ORA-12170 → Timeout
  - Cursor errors NOT retried

Acceptance:
- No shared base class between providers; no shared provider logic.

### 13) Facade executor (`DbExecutor`) (orchestrator only)
- Implement `DbExecutor` in `Database.Core/Execution`:
  - Not thread-safe (documented)
  - One executor = one logical operation
  - Lazy connection creation; open just-in-time; close on dispose
  - No finalizers
- Public API surface includes:
  - `ExecuteAsync`
  - `ExecuteScalarAsync<T>`
  - `QueryAsync<T>` (streaming)
  - Explicit materialization APIs (`QueryTablesAsync` / optional `QueryDataSetAsync`)
- SQL injection prevention:
  - Require parameters for values; do not encourage interpolated SQL.

Acceptance:
- Streaming query uses `DbDataReader` and does not buffer all rows.

### 14) Memory & GC checks (large app readiness)
- Ensure defaults are allocation-aware:
  - No per-row dictionaries
  - Avoid LINQ/closures in hot paths
  - Use typed getters by ordinal in mappers
  - Prefer sequential/forward-only reads (`SequentialAccess`) where appropriate
- Add an explicit note in docs: DataSet/DataTable are heavy and should be avoided for large results.

Acceptance:
- Example workloads do not materialize large result sets unless explicitly requested.

### 15) IntelliSense documentation (developer experience)
- Add XML docs to public APIs so “press `.`” shows descriptions:
  - Method purpose, parameter meanings, security rules, transaction/retry behavior.

Acceptance:
- Consumers see useful tooltips for key types/methods.

### 16) Tests (separate project)
- Add tests covering:
  - Positive/negative paths
  - Timeout + retry behavior (only allowed cases)
  - Transaction rollback on failure
  - Oracle cursor exhaustion scenario (as feasible)

Acceptance:
- `dotnet test` passes locally/CI.

### 17) Quality gates + packaging (every time)
- Enforce:
  - .NET analyzers on + warnings-as-errors
  - SonarQube analysis (no suppressions without justification)
  - `dotnet pack` / NuGet validation checks

Acceptance:
- CI fails on analyzer/Sonar violations or packaging issues.

---

## OPTIONAL FEATURE (AFTER CORE): BULK IMPORT WITH COLUMN MAPPING

This feature must be implemented only after the core library is complete and stable.

Goals:
- High-throughput inserts for large datasets
- Minimal memory usage (streaming input, batching, avoid large in-memory tables)
- Explicit column mapping (source → destination) with validation

Rules:
- Keep API provider-agnostic at the facade level (generic names, same usage across providers).
- Provider-specific bulk mechanisms live only inside Providers.
- Bulk import MUST be explicit (no hidden mode switches).
- Support cancellation and diagnostics.

Provider notes:
- SQL Server:
  - Use `SqlBulkCopy` for best performance; support explicit `ColumnMappings`.
  - Prefer streaming (e.g., `IDataReader`) over building `DataTable`.
- PostgreSQL:
  - Prefer `COPY` (binary or text) via provider capabilities; column list/order must be explicit.
  - Ensure safe handling (no string concatenation of values; use COPY protocol APIs).
- Oracle:
  - Use array binding / bulk copy mechanisms available in the Oracle provider.
  - Validate array sizes and handle cursor/resource limits carefully.

Validation requirements (FluentValidation when enabled):
- Destination table/schema name must be whitelisted/controlled (identifiers are not parameterizable).
- Column mapping must be complete and consistent (no duplicates, missing required columns).
- Data type compatibility checks where feasible (fail fast before sending huge payloads).

Result/diagnostics:
- Return inserted row count, duration, and any per-batch failures in the custom error/result contract.


## END OF DOCUMENT

Final statement (important)

Now, explicitly and undeniably, this document contains:
- ✅ A clear architecture profile
- ✅ A layered architecture description
- ✅ A complete, copyable folder structure
- ✅ Explicit dependency rules
- ✅ Zero ambiguity for Codex

If you say “yes, this is finally correct”, the next clean step is:
- Generate final Codex prompts from THIS document
- Or start STEP 1 coding
