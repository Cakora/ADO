# Error Handling + Retry (Plan)

This document is a **precise, step-by-step plan** to simplify error handling and retry logic while keeping the public API clean and extensible for future localization (resx).

## Goals (Non-Negotiable)

- Keep public surface small: **`DbCallerException` + `DbError`**.
- Support future localization via **`DbError.MessageKey` + `DbError.MessageParameters`** (middleware can translate via resx later).
- Retry is **opt-in** and **safe**:
  - Retry **buffered only**
  - Retry **never inside a transaction**
  - Retry **only when transient**
- Reduce code/duplication: make “what retries?” obvious without reading many rules.

## Current State (What Exists Today)

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

