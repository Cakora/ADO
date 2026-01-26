# Transactions (AdoAsync)

AdoAsync uses **explicit transactions** via `DbExecutor.BeginTransactionAsync()`. The returned `TransactionHandle` has **rollback-on-dispose** semantics: if you dispose the handle without committing, AdoAsync rolls back the transaction.

Key rules:

- `BeginTransactionAsync()` is **exclusive** per `DbExecutor` instance (no nesting / no parallel transactions on one executor).
- **Retries are disabled** while an explicit transaction is active (at-most-once behavior).
- All commands and bulk imports executed by that executor enlist automatically while the transaction is active.

---

## Single Transaction (recommended)

```csharp
using System.Data;
using AdoAsync;
using AdoAsync.Execution;

await using var executor = DbExecutor.Create(new DbOptions
{
    DatabaseType = DatabaseType.SqlServer,
    ConnectionString = "...",
    CommandTimeoutSeconds = 30,
    EnableValidation = true,
    EnableRetry = true // ignored while a user transaction is active
});

await using var tx = await executor.BeginTransactionAsync();

await executor.ExecuteAsync(new CommandDefinition
{
    CommandText = "update dbo.Items set Name = @name where Id = @id",
    CommandType = CommandType.Text,
    Parameters = new[]
    {
        new DbParameter { Name = "@id", DataType = DbDataType.Int32, Direction = ParameterDirection.Input, Value = 42 },
        new DbParameter { Name = "@name", DataType = DbDataType.String, Direction = ParameterDirection.Input, Size = 100, Value = "Updated" }
    }
});

await executor.ExecuteAsync(new CommandDefinition
{
    CommandText = "insert into dbo.AuditLog(ItemId, Action) values (@id, @action)",
    CommandType = CommandType.Text,
    Parameters = new[]
    {
        new DbParameter { Name = "@id", DataType = DbDataType.Int32, Direction = ParameterDirection.Input, Value = 42 },
        new DbParameter { Name = "@action", DataType = DbDataType.String, Direction = ParameterDirection.Input, Size = 50, Value = "Rename" }
    }
});

await tx.CommitAsync();
```

If any call throws, disposing `tx` without committing rolls back the whole transaction.

---

## Transaction + Output Parameters (complete example)

Output parameters work normally inside a transaction. Use the tuple-returning APIs like `ExecuteAsync(...)` and read `OutputParameters` (keys are prefix-trimmed: `@NewId`/`:NewId`/`?NewId` → `"NewId"`).

```csharp
using System.Collections.Generic;
using System.Data;
using AdoAsync;
using AdoAsync.Execution;

await using var executor = DbExecutor.Create(options);
await using var tx = await executor.BeginTransactionAsync();

(int RowsAffected, IReadOnlyDictionary<string, object?> OutputParameters) result =
    await executor.ExecuteAsync(new CommandDefinition
    {
        CommandText = "dbo.CreateCustomer",
        CommandType = CommandType.StoredProcedure,
        Parameters = new[]
        {
            new DbParameter { Name = "@Name", DataType = DbDataType.String, Direction = ParameterDirection.Input, Size = 200, Value = "Alice" },
            new DbParameter { Name = "@NewId", DataType = DbDataType.Int32, Direction = ParameterDirection.Output },
            new DbParameter { Name = "@ServerMessage", DataType = DbDataType.String, Direction = ParameterDirection.Output, Size = 4000 }
        }
    });

int newId = (int)(result.OutputParameters["NewId"] ?? 0);
string? message = (string?)result.OutputParameters["ServerMessage"];

await tx.CommitAsync();
```

Notes:

- Output string/binary parameters must specify `Size` (validation enforces this by default).
- “Complex” parameters (SQL Server TVP via `DbDataType.Structured`, Oracle array binding via `IsArrayBinding`) work inside transactions too.
- For streaming + outputs (SQL Server/PostgreSQL), use `ExecuteReaderAsync(...)` and read outputs **after** the reader is closed; see `docs/output-parameters.md`.

---

## Multiple Transactions

If you mean “multiple units of work”, start/commit transactions sequentially on the same executor:

```csharp
await using var executor = DbExecutor.Create(options);

await using (var tx = await executor.BeginTransactionAsync())
{
    await executor.ExecuteAsync(firstCommand);
    await tx.CommitAsync();
}

await using (var tx = await executor.BeginTransactionAsync())
{
    await executor.ExecuteAsync(secondCommand);
    await tx.CommitAsync();
}
```

If you mean “multiple transactions at the same time”, use **separate executors** (separate connections). Committing two independent DB transactions is not atomic without a distributed transaction coordinator.
