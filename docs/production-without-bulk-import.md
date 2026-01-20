# Production Build Without Bulk Import (No Code Changes Applied)

Use this checklist if you plan to ship without bulk import. It does not change code; it just lists what to adjust or verify.

## 1) Decide strategy
- **Option A (remove APIs):** Build/package without bulk-import files so the APIs disappear. This is cleanest for consumers.
- **Option B (keep APIs but unsupported):** Leave files in place but document that bulk import is not supported in your production build. Do not call these APIs.

## 2) Files that belong only to bulk import
- `src/AdoAsync/BulkImport/` (request/result/mapping types)
- `src/AdoAsync/BulkCopy/` (linq2db helpers)
- `src/AdoAsync/Validation/BulkImportRequestValidator.cs`
- Provider-specific bulk methods:
  - `src/AdoAsync/Providers/SqlServer/SqlServerProvider.cs` (`BulkImportAsync`)
  - `src/AdoAsync/Providers/PostgreSql/PostgreSqlProvider.cs` (`BulkImportAsync`)
  - `src/AdoAsync/Providers/Oracle/OracleProvider.cs` (`BulkImportAsync`)

## 3) Public surface that exposes bulk import
- `src/AdoAsync/Abstractions/IDbExecutor.cs` (`BulkImportAsync` overloads)
- `src/AdoAsync/Abstractions/IDbProvider.cs` (`BulkImportAsync`)
- `src/AdoAsync/Execution/Async/DbExecutor.cs` (three `BulkImportAsync` methods)

## 4) What to do based on your choice
- If removing bulk import: exclude the files in section 2 and remove the APIs in section 3 from your build/package.
- If keeping APIs but not using bulk import: leave code as-is and document that bulk import is not supported in production. Avoid calling these methods in production flows.

## 5) Documentation updates (recommended)
- In `src/AdoAsync/README.md`, add a note: “Bulk import is not supported in the production build” and remove bulk examples if you want to avoid confusion.

## 6) Packaging check
- Before shipping, inspect your package/binaries to confirm whether bulk files/APIs are included or intentionally absent, matching the strategy you chose above.
