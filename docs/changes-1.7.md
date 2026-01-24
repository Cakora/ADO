# AdoAsync v1.7 – Change Log (Draft)

Grouped by action.

## Added

- `docs/error-handling-retry.md` (error handling + retry plan; clarifies middleware responsibility for HTTP status + resx localization)

## Changed (Error message keys)

Provider exception mappers now use **generic** `DbError.MessageKey` values (shared across providers), so localization can be maintained with a single set of resx entries:

- `src/AdoAsync/Providers/SqlServer/SqlServerExceptionMapper.cs`
- `src/AdoAsync/Providers/PostgreSql/PostgreSqlExceptionMapper.cs`
- `src/AdoAsync/Providers/Oracle/OracleExceptionMapper.cs`

Examples of new generic keys:

- `errors.deadlock`
- `errors.timeout`
- `errors.connection_failure`
- `errors.resource_limit`
- `errors.syntax_error`

Breaking note (localization only):

- If your application had provider-specific resx entries (e.g., `errors.sqlserver.deadlock`), update them to the generic keys above.

## Refactor (Provider mappers)

- Simplified provider exception mappers to be “data-first” (small code/SQLSTATE lists) instead of rule-engine style matching.
- Removed `src/AdoAsync/Exceptions/ErrorRuleMatcher.cs` (no longer needed).
- Removed `src/AdoAsync/Exceptions/ExceptionHandler.cs` (directly maps via `ProviderHelper.MapProviderError`).
- Removed `src/AdoAsync/Exceptions/DbClientException.cs` (legacy alias; use `DbCallerException`).
