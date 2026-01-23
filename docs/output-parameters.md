# Output Parameters (How To Use)

This document shows the supported output-parameter patterns in AdoAsync, with complete input + output examples.

Key rules:

- Output dictionary keys are normalized (provider prefix trimmed): `@NewId` / `:NewId` / `?NewId` → `"NewId"`.
- Output values are normalized using the declared `DbDataType` when available.

---

## 1) Non-query + outputs (recommended when you only need outputs)

Use `IDbExecutor.ExecuteAsync(...)` (tuple return).

Example (stored procedure with an output parameter):

```csharp
using System.Collections.Generic;
using System.Data;
using AdoAsync;
using AdoAsync.Abstractions;
using AdoAsync.Execution;

await using IDbExecutor executor = DbExecutor.Create(options);

(int RowsAffected, IReadOnlyDictionary<string, object?> OutputParameters) result =
    await executor.ExecuteAsync(new CommandDefinition
    {
        CommandText = "dbo.CreateCustomer",
        CommandType = CommandType.StoredProcedure,
        Parameters = new[]
        {
            new DbParameter { Name = "@Name", DataType = DbDataType.String, Direction = ParameterDirection.Input, Value = "Alice" },
            new DbParameter { Name = "@NewId", DataType = DbDataType.Int32, Direction = ParameterDirection.Output }
        }
    });

int rowsAffected = result.RowsAffected;
int newId = (int)(result.OutputParameters["NewId"] ?? 0);
```

Notes:

- `OutputParameters` is never null on this method. If the command has no output parameters, it is an empty dictionary.
- This is supported for all providers (SQL Server / PostgreSQL / Oracle).

---

## 2) Scalar + outputs (when you need both a scalar and outputs)

Use `IDbExecutor.ExecuteScalarAsync<T>(...)` (tuple return).

Example (scalar result + output):

```csharp
using System.Collections.Generic;
using System.Data;
using AdoAsync;
using AdoAsync.Abstractions;
using AdoAsync.Execution;

await using IDbExecutor executor = DbExecutor.Create(options);

(int Value, IReadOnlyDictionary<string, object?> OutputParameters) result =
    await executor.ExecuteScalarAsync<int>(new CommandDefinition
    {
        CommandText = "dbo.GetNextSequenceValue",
        CommandType = CommandType.StoredProcedure,
        Parameters = new[]
        {
            new DbParameter { Name = "@SequenceName", DataType = DbDataType.String, Direction = ParameterDirection.Input, Value = "Customer" },
            new DbParameter { Name = "@ServerMessage", DataType = DbDataType.String, Direction = ParameterDirection.Output, Size = 4000 }
        }
    });

int value = result.Value;
string? message = (string?)result.OutputParameters["ServerMessage"];
```

Notes:

- If the scalar is `NULL`, `Value` is returned as `default(T)`.
- `OutputParameters` is never null on this method (empty dictionary when none exist).

---

## 3) Buffered tables/datasets (when you need rows + outputs)

Buffered APIs return output parameters in the tuple:

- `QueryTableAsync(...)` → `(..., OutputParameters)`
- `ExecuteDataSetAsync(...)` → `(..., OutputParameters)`

```csharp
using System.Collections.Generic;
using System.Data;
using AdoAsync;
using AdoAsync.Execution;

(DataTable Table, IReadOnlyDictionary<string, object?> OutputParameters) tableResult =
    await executor.QueryTableAsync(new CommandDefinition
{
    CommandText = "dbo.SelectCustomersAndReturnTotal",
    CommandType = CommandType.StoredProcedure,
    Parameters = new[]
    {
        new DbParameter { Name = "@minId", DataType = DbDataType.Int32, Direction = ParameterDirection.Input, Value = 100 },
        new DbParameter { Name = "@total", DataType = DbDataType.Int32, Direction = ParameterDirection.Output }
    }
});

DataTable table = tableResult.Table;
IReadOnlyDictionary<string, object?> outputs = tableResult.OutputParameters;
int? total = (int?)outputs["total"];
```

DataSet example (no `Tables[0]` required):

```csharp
using System.Collections.Generic;
using System.Data;
using AdoAsync;
using AdoAsync.Execution;

(DataSet DataSet, IReadOnlyDictionary<string, object?> OutputParameters) dataSetResult =
    await executor.ExecuteDataSetAsync(new CommandDefinition
{
    CommandText = "dbo.MultiResultProc",
    CommandType = CommandType.StoredProcedure,
    Parameters = new[]
    {
        new DbParameter { Name = "@status", DataType = DbDataType.Int32, Direction = ParameterDirection.Output }
    }
});

DataSet dataSet = dataSetResult.DataSet;
IReadOnlyDictionary<string, object?> outputs = dataSetResult.OutputParameters;
int? status = (int?)outputs["status"];
```

---

## 4) Streaming + outputs (SQL Server/PostgreSQL only)

Output parameters are not available until the reader is finished/closed.
Use `ExecuteReaderAsync` when you must stream rows and also need outputs (declare output parameters in `CommandDefinition.Parameters`).

```csharp
using System.Collections.Generic;
using System.Data;
using AdoAsync;
using AdoAsync.Execution;

await using StreamingReaderResult result = await executor.ExecuteReaderAsync(new CommandDefinition
{
    CommandText = "dbo.SelectCustomersAndReturnTotal",
    CommandType = CommandType.StoredProcedure,
    Parameters = new[]
    {
        new DbParameter { Name = "@minId", DataType = DbDataType.Int32, Direction = ParameterDirection.Input, Value = 100 },
        new DbParameter { Name = "@total", DataType = DbDataType.Int32, Direction = ParameterDirection.Output }
    }
});

await using (result.Reader)
{
    while (await result.Reader.ReadAsync())
    {
        // stream rows
    }
}

IReadOnlyDictionary<string, object?>? outputs = await result.GetOutputParametersAsync();
int? total = (int?)outputs?["total"];
```
