# ExecuteScalar + Output Parameters (All Providers)

This page shows complete, end-to-end examples of `ExecuteScalarAsync<T>` with output parameters.

Notes:

- Output dictionary keys are normalized (prefix trimmed): `@message` / `:message` / `?message` → `"message"`.
- Output string parameters should specify `Size`.
- PostgreSQL and Oracle require a stored procedure to return output parameters.

---

## SQL Server (no procedure required)

You can use `CommandType.Text` and set output parameters inside the SQL text.

```csharp
using System.Data;
using AdoAsync;
using AdoAsync.Abstractions;
using AdoAsync.Execution;

await using IDbExecutor executor = DbExecutor.Create(new DbOptions
{
    DatabaseType = DatabaseType.SqlServer,
    ConnectionString = "Server=.;Database=MyDb;Trusted_Connection=True;",
    CommandTimeoutSeconds = 30
});

(int Value, IReadOnlyDictionary<string, object?> OutputParameters) result =
    await executor.ExecuteScalarAsync<int>(new CommandDefinition
    {
        CommandText = @"
            select count(*) from dbo.Customers where Id >= @minId;
            set @message = N'OK';
        ",
        CommandType = CommandType.Text,
        Parameters = new[]
        {
            new DbParameter { Name = "@minId", DataType = DbDataType.Int32, Direction = ParameterDirection.Input, Value = 100 },
            new DbParameter { Name = "@message", DataType = DbDataType.String, Direction = ParameterDirection.Output, Size = 4000 }
        }
    });

int total = result.Value;
string? message = (string?)result.OutputParameters["message"];
```

---

## PostgreSQL (stored procedure required for output parameters)

PostgreSQL only supports output parameters on stored procedures. Use a procedure that returns a scalar via an OUT parameter.

Example procedure:

```sql
create or replace procedure public.get_customer_count_with_message(
    in p_min_id int,
    out p_total int,
    out p_message text
)
language plpgsql
as $$
begin
    select count(*) into p_total from public.customers where id >= p_min_id;
    p_message := 'OK';
end;
$$;
```

Usage:

```csharp
using System.Data;
using AdoAsync;
using AdoAsync.Abstractions;
using AdoAsync.Execution;

await using IDbExecutor executor = DbExecutor.Create(new DbOptions
{
    DatabaseType = DatabaseType.PostgreSql,
    ConnectionString = "Host=localhost;Database=mydb;Username=myuser;Password=mypassword",
    CommandTimeoutSeconds = 30
});

(int Value, IReadOnlyDictionary<string, object?> OutputParameters) result =
    await executor.ExecuteScalarAsync<int>(new CommandDefinition
    {
        CommandText = "public.get_customer_count_with_message",
        CommandType = CommandType.StoredProcedure,
        Parameters = new[]
        {
            new DbParameter { Name = "p_min_id", DataType = DbDataType.Int32, Direction = ParameterDirection.Input, Value = 100 },
            new DbParameter { Name = "p_total", DataType = DbDataType.Int32, Direction = ParameterDirection.Output },
            new DbParameter { Name = "p_message", DataType = DbDataType.String, Direction = ParameterDirection.Output, Size = 4000 }
        }
    });

int total = result.Value;
string? message = (string?)result.OutputParameters["p_message"];
```

---

## Oracle (stored procedure required for output parameters)

Use a stored procedure with OUT parameters (not a return value).

Example procedure:

```sql
create or replace procedure get_customer_count_with_message(
    p_min_id in number,
    p_total out number,
    p_message out varchar2
) as
begin
    select count(*) into p_total from customers where id >= p_min_id;
    p_message := 'OK';
end;
/
```

Usage:

```csharp
using System.Data;
using AdoAsync;
using AdoAsync.Abstractions;
using AdoAsync.Execution;

await using IDbExecutor executor = DbExecutor.Create(new DbOptions
{
    DatabaseType = DatabaseType.Oracle,
    ConnectionString = "User Id=myuser;Password=mypassword;Data Source=MyOracleDb",
    CommandTimeoutSeconds = 30
});

(int Value, IReadOnlyDictionary<string, object?> OutputParameters) result =
    await executor.ExecuteScalarAsync<int>(new CommandDefinition
    {
        CommandText = "get_customer_count_with_message",
        CommandType = CommandType.StoredProcedure,
        Parameters = new[]
        {
            new DbParameter { Name = "p_min_id", DataType = DbDataType.Int32, Direction = ParameterDirection.Input, Value = 100 },
            new DbParameter { Name = "p_total", DataType = DbDataType.Int32, Direction = ParameterDirection.Output },
            new DbParameter { Name = "p_message", DataType = DbDataType.String, Direction = ParameterDirection.Output, Size = 4000 }
        }
    });

int total = result.Value;
string? message = (string?)result.OutputParameters["p_message"];
```
