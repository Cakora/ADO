# Changes 1.2

## Added
- src/AdoAsync/Abstractions/StreamingReaderResult.cs:1-77
- src/AdoAsync/Exceptions/ExceptionHandler.cs:1-24
- src/AdoAsync/Extensions/Execution/DataTableOutputExtensions.cs:1-38
- src/AdoAsync/Validation/Localization/ResxLanguageManager.cs:1-37
- tests/AdoAsync.Tests/OutputParameterExtensionsTests.cs:1-54

## Updated
- src/AdoAsync/Abstractions/IDbExecutor.cs:16-35
- src/AdoAsync/Execution/Async/DbExecutor.cs:29-480
- src/AdoAsync/Validation/ValidationRunner.cs:11-94
- docs/IDbExecutor-methods.md:1-15
- tests/AdoAsync.Tests/ValidationTests.cs:300-347

## Moved
- src/AdoAsync/Core/DbError.cs ➜ src/AdoAsync/Exceptions/DbError.cs:1-75
- src/AdoAsync/Core/DbErrorMapper.cs ➜ src/AdoAsync/Exceptions/DbErrorMapper.cs
- src/AdoAsync/Core/ErrorRuleMatcher.cs ➜ src/AdoAsync/Exceptions/ErrorRuleMatcher.cs

## Deleted
- src/AdoAsync/Core/DataSetResult.cs
