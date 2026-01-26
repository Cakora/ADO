# AdoAsync v1.8 — Change Log

This file consolidates v1.7b + v1.8 notes into a single changelog.

Grouped by action. Each bullet includes `path:line` so you can jump directly to the change.

## Breaking

- `src/AdoAsync/Exceptions/DbError.cs:15` — `DbError.Code` is now `string` (stable code text).
- `src/AdoAsync/Exceptions/DbError.cs:60` — `DbErrorCodes.*` string constants replace the old `DbErrorCode` enum.
- `src/AdoAsync/Exceptions/DbCallerException.cs:21` — `DbCallerException.ErrorCode` is now `string` (compare with `DbErrorCodes.*`).

## Added

- `docs/changes-1.8.md:1` — this file.
- `docs/bulk-update-staging.md:1` — “send once” bulk update patterns (XML / SQL Server TVP / Oracle GTT+arrays), includes transaction usage.
- `src/AdoAsync/Providers/SqlServer/SqlServerParameterExtensions.cs:8` — `ToTvp(...)` helper for SQL Server TVP parameters.
- `src/AdoAsync/Providers/Oracle/OracleArrayBindingExtensions.cs:10` — `ToArrayBindingParameter(...)` helpers for Oracle associative array binding.
- `tests/AdoAsync.Tests/ProviderParameterBindingTests.cs:11` — positive/negative tests for TVP + Oracle array binding provider wiring.
- `tests/AdoAsync.Tests/ParameterValidationTests.cs:8` — positive/negative tests for new parameter validation rules.

## Updated

### Parameter Binding (TVP + Oracle Arrays)

- `src/AdoAsync/Core/DbDataType.cs:78` — added `DbDataType.Structured` for SQL Server TVP.
- `src/AdoAsync/Core/DbParameter.cs:43` — added `StructuredTypeName` (TVP type name).
- `src/AdoAsync/Core/DbParameter.cs:51` — added `IsArrayBinding` (Oracle associative arrays).
- `src/AdoAsync/Providers/SqlServer/SqlServerTypeMapper.cs:55` — maps `DbDataType.Structured` → `SqlDbType.Structured`.
- `src/AdoAsync/Providers/SqlServer/SqlServerProvider.cs:57` — binds TVP (`SqlParameter.TypeName`) and enforces input-only.
- `src/AdoAsync/Providers/Oracle/OracleProvider.cs:62` — binds associative arrays (`CollectionType`, `ArrayBindCount`, per-element `ArrayBindSize` for strings).
- `src/AdoAsync/Validation/DbParameterValidator.cs:70` — validates TVP requirements (Input + `StructuredTypeName` + value).
- `src/AdoAsync/Validation/DbParameterValidator.cs:78` — validates array binding requirements (Input + non-empty array; string arrays require `Size`).

### Error Contract + Mapping

- `src/AdoAsync/Exceptions/DbError.cs:60` — `DbErrorCodes.*` includes stable codes like `AuthenticationFailed`, `Canceled`, `GenericTimeout`, `GenericDeadlock`.
- `src/AdoAsync/Exceptions/DbErrorMapper.cs:18` — `FromProvider(..., string code, ...)` (provider mappers emit string codes).
- `src/AdoAsync/Exceptions/DbErrorMapper.cs:67` — `Map(..., string? providerCode = null, ...)` uses string codes.
- `src/AdoAsync/Providers/SqlServer/SqlServerExceptionMapper.cs:47` — uses `DbErrorCodes.*` (string codes).
- `src/AdoAsync/Providers/PostgreSql/PostgreSqlExceptionMapper.cs:48` — uses `DbErrorCodes.*` (string codes).
- `src/AdoAsync/Providers/Oracle/OracleExceptionMapper.cs:47` — uses `DbErrorCodes.*` (string codes).
- `src/AdoAsync/Validation/ValidationRunner.cs:88` — validation errors use `DbErrorCodes.ValidationFailed`.

### Docs

- `src/AdoAsync/README.md:33` — updated error handling example to compare with `DbErrorCodes.*`.
- `docs/error-handling-retry.md:12` — docs refer to `DbErrorType` + `DbError.Code` (string) for middleware handling.
- `docs/error-handling-retry.md:62` — middleware example uses `DbErrorCodes.AuthenticationFailed`.
- `docs/changes-1.7.md:41` — references updated to `DbErrorCodes.*` (kept for v1.7 history).

## Removed

- `src/AdoAsync/Exceptions/DbError.cs:60` — removed `DbErrorCode` enum (replaced by `DbErrorCodes` constants).
- `docs/changes-1.7b.md:1` — merged into `docs/changes-1.8.md`.

## Notes (Why string error codes)

- `DbError.Code` as a stable `string` makes middleware/localization simple: map `ex.Error.Code` → HTTP status + resx key without depending on provider exception types.
