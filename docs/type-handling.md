# Type Handling and Normalization

This document explains how AdoAsync keeps value handling consistent across SQL Server, PostgreSQL, and Oracle.

---

## 1) Parameter Types: `DbDataType`

When sending parameters, you declare a `DbDataType`:

```csharp
new DbParameter
{
    Name = "@id",
    DataType = DbDataType.Int32,
    Direction = ParameterDirection.Input,
    Value = 42
}
```

Each provider maps `DbDataType` to its native type:

- SQL Server → `SqlDbType` (via `SqlServerTypeMapper`)
- PostgreSQL → `NpgsqlDbType` (via `PostgreSqlTypeMapper`)
- Oracle → `OracleDbType` (via `OracleTypeMapper`)

This mapping is used only for **parameters**. It does not force result-set column types.

---

## 2) Output Parameters: normalization rules

When AdoAsync extracts output parameters, it:

1. Skips `Direction = Input`
2. Skips `DbDataType.RefCursor` outputs (those are treated as result sets)
3. Normalizes output values by their declared `DbDataType` when possible
4. Trims prefixes from names for dictionary keys (`@`, `:`, `?`)

Examples:

- Declare `@total` (SQL Server) → outputs dictionary key `total`
- Declare `:p_status` (Oracle) → outputs dictionary key `p_status`

Output parameters are surfaced on buffered methods via:

- `DataTable.ExtendedProperties["OutputParameters"]`
- `DataSet.ExtendedProperties["OutputParameters"]`

Retrieve them via:

- `AdoAsync.Extensions.Execution.OutputParameterExtensions.GetOutputParameters(DataTable)`
- `AdoAsync.Extensions.Execution.OutputParameterExtensions.GetOutputParameters(DataSet)`

For streaming reader + outputs (SQL Server/PostgreSQL only), use:

- `ExecuteReaderWithOutputsAsync` → `StreamingReaderResult.GetOutputParametersAsync()`

---

## 3) Reading Result Values: recommended approach

### `AdoAsync.Common.DataRecordExtensions.Get<T>`

When streaming rows (`IDataRecord` / `DbDataReader`), the recommended pattern is:

```csharp
using AdoAsync.Common;

var id = record.Get<int>("id");
var name = record.Get<string>("name");
var created = record.Get<DateTime>("created_at");
```

Behavior:

- Returns `default` for `DBNull` / null values
- If the raw value is already `T`, returns it directly
- Otherwise attempts a safe conversion for common scenarios:
  - `Guid` from `Guid` / `byte[16]` / string
  - `DateTimeOffset` from `DateTime` (kind normalized)
  - `TimeSpan` from `TimeSpan` or `DateTime.TimeOfDay`
  - Enums from string or numeric values

This keeps common provider differences out of your mapping code.

---

## 4) Common Provider Differences to Expect

These are common cases across ADO.NET providers that you should plan for:

### Booleans

- Some providers may return numeric representations (`0`/`1`) instead of `bool`.
- When reading via `Get<bool>`, conversion logic is applied when possible.

### GUID / UUID

- PostgreSQL commonly returns `uuid` as `Guid`.
- Some providers or drivers can return GUID as `string` or `byte[]`.

### Date/Time

- Oracle and SQL Server often return `DateTime`.
- PostgreSQL may return `DateTime` or `DateTimeOffset` depending on column type (`timestamp` vs `timestamptz`).

### Unsigned types

- Many database types do not have true unsigned integer types.
- `DbDataType.UInt64` is mapped conservatively (often via `decimal`) to avoid overflow.

---

## 5) Practical Guidance

- Prefer `IDataRecord` + `Get<T>` when streaming rows.
- For output parameters, always set `Size` for output strings/binary.
- If you need output parameters from a procedure that does not return result sets, call `QueryTableAsync` (it can return an empty table and still surface outputs).
- Use `DbDataType.RefCursor` only for PostgreSQL/Oracle refcursor outputs, and consume them via `QueryTablesAsync` / `QueryTableAsync`.

