# Error Handling + Retry (Plan)

This document is a **precise, step-by-step plan** to simplify error handling and retry logic while keeping the public API clean and extensible for future localization (resx).

## Problem We’re Solving (Why This Exists)

We want **clean code** with **no “1000-line error layer”** inside the library.

The design target is:

- Library code stays small and consistent.
- API apps can handle errors in ~10 lines using `DbErrorType`/`DbErrorCode`.
- Localization stays outside the library (middleware can use resx when needed).

## Goals (Non-Negotiable)

- Keep public surface small: **`DbCallerException` + `DbError`**.
- Support future localization via **`DbError.MessageKey` + `DbError.MessageParameters`** (middleware can translate via resx later).
- Retry is **opt-in** and **safe**:
  - Retry **buffered only**
  - Retry **never inside a transaction**
  - Retry **only when transient**
- Reduce code/duplication: make “what retries?” obvious without reading many rules.

## Current State (What Exists Today)

### Where HTTP status + localized message should happen

This library is provider-agnostic and runtime-agnostic (API, worker, console), so it **must throw** exceptions and cannot decide HTTP response codes.

Recommended separation:

- **AdoAsync (library):** throws `DbCallerException` containing `DbError` (`Type`/`Code`/`MessageKey`/`MessageParameters`/`IsTransient`).
- **Application (API middleware):** converts `DbError` into:
  - HTTP status code (based on `DbErrorType`),
  - localized message (resx lookup using `MessageKey`),
  - response payload shape (your API contract).

To keep middleware short and “list-driven”, prefer a dictionary for status mapping (instead of large switch blocks):

```csharp
static readonly IReadOnlyDictionary<DbErrorType, int> StatusByType = new Dictionary<DbErrorType, int>
{
    [DbErrorType.ValidationError] = StatusCodes.Status400BadRequest,
    [DbErrorType.SyntaxError] = StatusCodes.Status400BadRequest,
    [DbErrorType.Canceled] = 499, // Client Closed Request (commonly used; not in StatusCodes)
    [DbErrorType.Timeout] = StatusCodes.Status504GatewayTimeout,
    [DbErrorType.ConnectionFailure] = StatusCodes.Status503ServiceUnavailable,
    [DbErrorType.Deadlock] = StatusCodes.Status409Conflict,
    [DbErrorType.ResourceLimit] = StatusCodes.Status429TooManyRequests
};
```

### Minimal “10-line” middleware shape (target)

The goal is that your API layer can do something like this (pseudo-code):

```csharp
catch (DbCallerException ex)
{
    // Special-case auth failures by stable code (more precise than Type).
    var status = ex.Error.Code switch
    {
        DbErrorCode.AuthenticationFailed => StatusCodes.Status401Unauthorized,
        _ => StatusByType.GetValueOrDefault(ex.Error.Type, 500)
    };

    var message = Localize(ex.Error.MessageKey, ex.Error.MessageParameters); // resx later
    return Problem(status, ex.Error.Code.ToString(), message);
}
```

This stays tiny because the library already provides the meaning:

- `DbErrorType` → which kind of failure
- `DbErrorCode` → stable code for client logic
- `MessageKey` → stable localization key (optional now, useful later)
- `IsTransient` → retry hint (buffered only, no transaction)

### Retry behavior

- Policy is created in `DbExecutor.Create(...)` via `RetryPolicyFactory.Create(...)`.
- Retry runs only through `ExecuteWithRetryIfAllowedAsync(...)`.
- Retry is disabled when:
  - `_activeTransaction != null` (explicit transaction active), or
  - `DbOptions.EnableRetry == false`.
- Streaming is not retried (by design).

### Error mapping behavior

- Provider errors are mapped to `DbError` via provider-specific mappers:
  - SQL Server: `SqlException.Number`
  - PostgreSQL: `PostgresException.SqlState`
  - Oracle: `OracleException.Number`
- These mappers determine:
  - `DbError.Type` / `DbError.Code`
  - `DbError.MessageKey`
  - `DbError.IsTransient` (this drives retry)
  - `DbError.ProviderDetails` (safe diagnostics)

## Step-by-Step Plan

### 1) Inventory + Analysis (DO THIS FIRST; NO CODE CHANGES)

**Objective:** understand exactly what’s in place and what is duplicated.

1. List all error-handling files and their responsibilities:
   - `src/AdoAsync/Exceptions/*`
   - `src/AdoAsync/Providers/*/*ExceptionMapper.cs`
   - `src/AdoAsync/Resilience/RetryPolicyFactory.cs`
   - Any retry entrypoints in `src/AdoAsync/Execution/Async/*`
2. Extract the current “retryable signals” per provider into a table:
   - SQL Server: list of `SqlException.Number` treated as transient + which type they map to (timeout/deadlock/connection/etc.).
   - PostgreSQL: list of `SqlState` values treated as transient + which type they map to.
   - Oracle: list of `OracleException.Number` treated as transient + which type they map to.
3. Verify where retry actually applies:
   - Identify each method that calls `ExecuteWithRetryIfAllowedAsync(...)` (buffered paths).
   - Confirm that streaming paths do not use retry.
4. Identify duplication:
   - Places where we compute “transient?” in more than one way.
   - Places where we map “type/message key” and “transient” separately.

**Deliverables (end of step 1):**
- `RetryableSignals` table (per provider).
- List of “retry-enabled” methods.
- List of duplicated patterns to remove.

### 1.1) Identify the “minimum set” (stop the complexity early)

**Objective:** prevent over-engineering before refactor begins.

1. Decide which error types we *actually* need for retry + API mapping:
   - Usually: Timeout, Deadlock, ConnectionFailure, ResourceLimit (transient)
   - Usually: ValidationError, SyntaxError (non-transient)
   - Everything else → Unknown
2. Anything outside that set must justify itself:
   - If it doesn’t change retry decision or API behavior, don’t add it.

### 2) Define the Minimal Target Design (WRITE THIS DOWN BEFORE REFACTOR)

**Objective:** agree on a minimal and consistent architecture.

1. Decide the stable shared error types we support (already in `DbErrorType`):
   - Timeout, Deadlock, ConnectionFailure, ResourceLimit, SyntaxError, ValidationError, Unknown
2. Define a single rule:
   - `IsTransient` should be derived from the **shared error type**, not sprinkled across code.
   - Example default: transient = Timeout/Deadlock/ConnectionFailure/ResourceLimit, non-transient = Validation/Syntax/Unknown (unless explicitly decided otherwise).
3. Define message key rules:
   - Prefer a small set of stable keys.
   - Provider-specific keys are allowed, but should be minimal and documented.

**Deliverables (end of step 2):**
- A written “transient by type” policy.
- A written list of message keys to keep.

### 3) Create “Transient + Type Classifier” per Provider (SMALL, READABLE)

**Objective:** make the “retry list” obvious.

1. For each provider, create a **single small classifier** that answers:
   - “What shared error type is this?” (Timeout/Deadlock/ConnectionFailure/…)
   - Based primarily on codes (`Number` / `SqlState`), with **message-text fallback only when code is missing**.
2. The classifier should be data-first:
   - a small set/list of retryable codes/SQLSTATE
   - one place to read and update

**Deliverables (end of step 3):**
- One classifier per provider with a short, obvious list of codes.

### 4) Make `DbError` Construction Centralized (ONE BUILDER)

**Objective:** remove duplication when constructing `DbError`.

1. Centralize building of `DbError` fields:
   - Type, Code, MessageKey, MessageParameters, ProviderDetails, IsTransient
2. Provider mappers become thin:
   - classify → build → return

**Deliverables (end of step 4):**
- Provider mappers are short and consistent.

### 5) Keep Retry Wiring Simple (NO CHANGE IN BEHAVIOR)

**Objective:** keep existing retry boundaries intact.

1. Keep `ExecuteWithRetryIfAllowedAsync` rules:
   - no retry inside transactions
   - retry opt-in via `EnableRetry`
2. Ensure `isTransient(exception)` uses the classifier output (and the shared transient-by-type policy).

**Deliverables (end of step 5):**
- Retry behavior unchanged, but classification clearer.

### 6) Documentation: Make Retry Rules Visible Without Reading Code

**Objective:** “no one reads 50 lines to know what retries”.

1. Add a table to this file:
   - Provider → Code/SQLSTATE → Mapped `DbErrorType` → Retry? (yes/no)
2. Document the two hard rules:
   - no retry in transactions
   - no retry for streaming

**Deliverables (end of step 6):**
- “Retry Matrix” table in this doc.

#### Retry Matrix (current mapping)

This table is the single “list” to understand retry classification without reading code.

| Provider | Signal | Example values | Mapped `DbErrorType` | `IsTransient` (retry hint) | MessageKey |
|---|---|---|---|---:|---|
| SQL Server | `SqlException.Number` | `1205` | `Deadlock` | true | `errors.deadlock` |
| SQL Server | `SqlException.Number` | `-2` | `Timeout` | true | `errors.timeout` |
| SQL Server | `SqlException.Number` | `10928`, `10929` | `ResourceLimit` | true | `errors.resource_limit` |
| SQL Server | `SqlException.Number` | `4060`, `18456` | `ConnectionFailure` | false (override) | `errors.authentication_failed` |
| SQL Server | message fallback | contains `"transport-level error"` | `ConnectionFailure` | true | `errors.connection_failure` |
| PostgreSQL | `PostgresException.SqlState` | `28P01` | `ConnectionFailure` | false (override) | `errors.authentication_failed` |
| PostgreSQL | `PostgresException.SqlState` | `40P01` | `Deadlock` | true | `errors.deadlock` |
| PostgreSQL | `PostgresException.SqlState` | `40001` | `Deadlock` | true | `errors.deadlock` |
| PostgreSQL | `PostgresException.SqlState` | `57014` | `Timeout` | true | `errors.timeout` |
| PostgreSQL | `PostgresException.SqlState` | `08000` | `ConnectionFailure` | true | `errors.connection_failure` |
| PostgreSQL | `PostgresException.SqlState` | `55P03` | `ResourceLimit` | true | `errors.resource_limit` |
| PostgreSQL | `PostgresException.SqlState` | `42601` | `SyntaxError` | false | `errors.syntax_error` |
| PostgreSQL | message fallback | contains `"terminating connection"` | `ConnectionFailure` | true | `errors.connection_failure` |
| Oracle | `OracleException.Number` | `1017` | `ConnectionFailure` | false (override) | `errors.authentication_failed` |
| Oracle | `OracleException.Number` | `1013`, `12170` | `Timeout` | true | `errors.timeout` |
| Oracle | `OracleException.Number` | `12514`, `12541` | `ConnectionFailure` | true | `errors.connection_failure` |
| Oracle | `OracleException.Number` | `1000` | `ResourceLimit` | false (override) | `errors.resource_limit` |
| Oracle | message fallback | contains `"broken pipe"` | `ConnectionFailure` | true | `errors.connection_failure` |

### 7) Validation + Safety Checks

**Objective:** ensure we didn’t break behavior.

1. Run build + tests.
2. Add/adjust unit tests only if missing coverage for:
   - “transient classification” per provider mapper
   - “no retry when transaction active”

**Deliverables (end of step 7):**
- Passing test suite and clear behavior guarantees.

## Rules (Keep It Clean)

- Don’t create many custom exception types. Prefer `DbCallerException` + `DbError`.
- Avoid message-text parsing except as fallback.
- Retry logic is for **idempotent buffered operations only** and must remain opt-in.
- All retryable signals must be visible in one place (classifier + this doc).

## Non-Goals (What We Will Not Do)

- No huge “error framework” inside the library.
- No API-specific dependencies (no HTTP types, no middleware inside AdoAsync).
- No retry for streaming.
- No retry inside transactions.
