# Complete Query Examples (All Providers)

This page gives full, end-to-end examples for:

- `QueryAsync<T>` → returns a buffered `List<T>` plus `OutputParameters`.
- `QueryTablesAsync` → returns multiple `DataTable` results plus `OutputParameters`.

Each example includes setup, executor creation, command definition, output reads, and disposal.

Notes:

- Output dictionary keys are normalized (prefix trimmed): `@message` / `:message` / `?message` → `"message"`.
- Output string parameters should specify `Size`.
- Refcursor parameters (`DbDataType.RefCursor`) produce result tables and are not included in `OutputParameters`.
- You can build commands either with `CommandDefinitionFactory.Create(...)` or directly with `new CommandDefinition { ... }` (both are supported).

---

## Shared model (used in `QueryAsync<T>` examples)

```csharp
public sealed record Customer(int Id, string Name);
```

---

## Shared parameter list (avoid duplication)

First create a `List<DbParameterSpec>`, then create a `CommandDefinition` with a generic factory call.

```csharp
using System.Collections.Generic;
using System.Data;
using AdoAsync;

var parameterSpecs = new List<CommandDefinitionFactory.DbParameterSpec>
{
    new("@customerId", DbDataType.Int32, ParameterDirection.Input, 42),
    new("@message", DbDataType.String, ParameterDirection.Output, Size: 4000)
};

var sqlServerSpecs = new List<CommandDefinitionFactory.DbParameterSpec>
{
    new("@customerId", DbDataType.Int32, ParameterDirection.Input, 42),
    new("@message", DbDataType.String, ParameterDirection.Output, Size: 4000)
};

var postgreSqlSpecs = new List<CommandDefinitionFactory.DbParameterSpec>
{
    new("customer_id", DbDataType.Int32, ParameterDirection.Input, 42),
    new("customer_cursor", DbDataType.RefCursor, ParameterDirection.Output),
    new("message", DbDataType.String, ParameterDirection.Output, Size: 4000)
};

var oracleSpecs = new List<CommandDefinitionFactory.DbParameterSpec>
{
    new("p_customer_id", DbDataType.Int32, ParameterDirection.Input, 42),
    new("p_customer", DbDataType.RefCursor, ParameterDirection.Output),
    new("p_message", DbDataType.String, ParameterDirection.Output, Size: 4000)
};

var sqlServerProc = CommandDefinitionFactory.Create(
    commandText: "dbo.GetCustomerAndOrdersWithStatus",
    commandType: CommandType.StoredProcedure,
    parameterSpecs: sqlServerSpecs,
    commandTimeoutSeconds: 30);

var postgreSqlProc = CommandDefinitionFactory.Create(
    commandText: "public.get_customers_with_status",
    commandType: CommandType.StoredProcedure,
    parameterSpecs: postgreSqlSpecs,
    commandTimeoutSeconds: 30);

var sqlServerText = CommandDefinitionFactory.Create(
    commandText: "select Id, Name from dbo.Customers where Id >= @minId",
    commandType: CommandType.Text,
    parameterSpecs: new List<CommandDefinitionFactory.DbParameterSpec>
    {
        new("@minId", DbDataType.Int32, ParameterDirection.Input, 100)
    },
    commandTimeoutSeconds: 30);
```

### Provider-specific parameter specs

SQL Server (uses `@` prefix):

```csharp
var sqlServerSpecs = new List<CommandDefinitionFactory.DbParameterSpec>
{
    new("@customerId", DbDataType.Int32, ParameterDirection.Input, 42),
    new("@message", DbDataType.String, ParameterDirection.Output, Size: 4000)
};
```

PostgreSQL (no prefix, refcursor for result sets):

```csharp
var postgreSqlSpecs = new List<CommandDefinitionFactory.DbParameterSpec>
{
    new("customer_id", DbDataType.Int32, ParameterDirection.Input, 42),
    new("customer_cursor", DbDataType.RefCursor, ParameterDirection.Output),
    new("message", DbDataType.String, ParameterDirection.Output, Size: 4000)
};
```

Oracle (no prefix, refcursor for result sets):

```csharp
var oracleSpecs = new List<CommandDefinitionFactory.DbParameterSpec>
{
    new("p_customer_id", DbDataType.Int32, ParameterDirection.Input, 42),
    new("p_customer", DbDataType.RefCursor, ParameterDirection.Output),
    new("p_message", DbDataType.String, ParameterDirection.Output, Size: 4000)
};
```

---

## Minimal helper (doc-only) for parameter prefixes

If you want a tiny helper without changing the library, use this in your app to apply provider prefixes:

```csharp
using System.Collections.Generic;
using System.Linq;
using AdoAsync;

static string Prefix(DatabaseType db, string name)
{
    if (string.IsNullOrWhiteSpace(name))
    {
        return name;
    }

    return db switch
    {
        DatabaseType.SqlServer => name.StartsWith("@") ? name : "@" + name,
        DatabaseType.PostgreSql => name.TrimStart('@', ':', '?'),
        DatabaseType.Oracle => name.TrimStart('@', ':', '?'),
        _ => name
    };
}

static List<CommandDefinitionFactory.DbParameterSpec> PrefixSpecs(DatabaseType db, IEnumerable<CommandDefinitionFactory.DbParameterSpec> specs)
    => specs.Select(s => s with { Name = Prefix(db, s.Name) }).ToList();
```

You can also let the factory normalize prefixes for you:

```csharp
var command = CommandDefinitionFactory.Create(
    databaseType: DatabaseType.SqlServer,
    commandText: "dbo.GetCustomerAndOrdersWithStatus",
    commandType: CommandType.StoredProcedure,
    parameterSpecs: new[]
    {
        new CommandDefinitionFactory.DbParameterSpec("customerId", DbDataType.Int32, ParameterDirection.Input, 42),
        new CommandDefinitionFactory.DbParameterSpec("message", DbDataType.String, ParameterDirection.Output, Size: 4000)
    },
    commandTimeoutSeconds: 30);
```

The `databaseType` argument handles `@` prefixing for SQL Server and trims prefixes for PostgreSQL/Oracle, so you can pass bare names like `"customerId"`.

## Complete CommandDefinition (all fields set)

This example shows every available field: command text/type, parameters, timeout, behavior, and identifier validation.

```csharp
using System.Collections.Generic;
using System.Data;
using AdoAsync;

var parameterSpecs = new List<CommandDefinitionFactory.DbParameterSpec>
{
    new("@minId", DbDataType.Int32, ParameterDirection.Input, 100),
    new("@status", DbDataType.Int32, ParameterDirection.Output, Size: 4),
    new("@message", DbDataType.String, ParameterDirection.Output, Size: 4000),
    new("@price", DbDataType.Decimal, ParameterDirection.Input, 12.34m, Precision: 10, Scale: 2),
    new("@tvp", DbDataType.Structured, ParameterDirection.Input, Value: null, StructuredTypeName: "dbo.ItemType"),
    new("@arrayValues", DbDataType.Int32, ParameterDirection.Input, new[] { 1, 2, 3 }, IsArrayBinding: true)
};

var command = CommandDefinitionFactory.Create(
    commandText: "dbo.GetItemsWithStatus",
    commandType: CommandType.StoredProcedure,
    parameterSpecs: parameterSpecs,
    commandTimeoutSeconds: 60,
    behavior: CommandBehavior.Default,
    allowedIdentifiers: new HashSet<string> { "dbo.Items", "dbo.ItemStatus" },
    identifiersToValidate: new List<string> { "dbo.Items" });
```

---

## SQL Server

### A) `QueryAsync<T>` (list + output message)

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

var command = CommandDefinitionFactory.Create(
    databaseType: DatabaseType.SqlServer,
    commandText: "dbo.GetCustomersWithStatus",
    commandType: CommandType.StoredProcedure,
    parameterSpecs: new[]
    {
        new CommandDefinitionFactory.DbParameterSpec("minId", DbDataType.Int32, ParameterDirection.Input, 100),
        new CommandDefinitionFactory.DbParameterSpec("message", DbDataType.String, ParameterDirection.Output, Size: 4000)
    });

(List<Customer> Rows, IReadOnlyDictionary<string, object?> OutputParameters) result =
    await executor.QueryAsync(
        command,
        row => new Customer(
            Id: row.Field<int>("Id"),
            Name: row.Field<string>("Name") ?? ""));

List<Customer> customers = result.Rows;
string? message = (string?)result.OutputParameters["message"];
```

Example method using `ExecuteAsync` with a `CommandDefinition`:

```csharp
public static async Task<(int RowsAffected, IReadOnlyDictionary<string, object?> OutputParameters)> UpdateCustomerStatusAsync(
    IDbExecutor executor,
    int customerId,
    string status)
{
    var command = CommandDefinitionFactory.Create(
        databaseType: DatabaseType.SqlServer,
        commandText: "dbo.UpdateCustomerStatus",
        commandType: CommandType.StoredProcedure,
        parameterSpecs: new[]
        {
            new CommandDefinitionFactory.DbParameterSpec("customerId", DbDataType.Int32, ParameterDirection.Input, customerId),
            new CommandDefinitionFactory.DbParameterSpec("status", DbDataType.String, ParameterDirection.Input, status),
            new CommandDefinitionFactory.DbParameterSpec("message", DbDataType.String, ParameterDirection.Output, Size: 4000)
        },
        commandTimeoutSeconds: 30);

    return await executor.ExecuteAsync(command);
}
```

Example: create list, single-line factory call, then use command in a method to process `DataTable`:

```csharp
var tableSpecs = new List<CommandDefinitionFactory.DbParameterSpec>
{
    new CommandDefinitionFactory.DbParameterSpec("minId", DbDataType.Int32, ParameterDirection.Input, 100)
};

var tableCommand = CommandDefinitionFactory.Create(commandText: "dbo.GetCustomers", commandType: CommandType.StoredProcedure, parameterSpecs: tableSpecs, databaseType: DatabaseType.SqlServer, commandTimeoutSeconds: 30);

public static async Task<List<Customer>> LoadCustomersAsync(IDbExecutor executor, CommandDefinition command)
{
    (DataTable Table, IReadOnlyDictionary<string, object?> OutputParameters) tableResult =
        await executor.QueryTableAsync(command);

    return tableResult.Table.ToList(row => new Customer(
        Id: row.Field<int>("Id"),
        Name: row.Field<string>("Name") ?? ""));
}
```

Example: `ExecuteDataSetAsync` (multiple tables):

```csharp
var dataSetCommand = CommandDefinitionFactory.Create(
    databaseType: DatabaseType.SqlServer,
    commandText: "dbo.GetCustomersAndOrders",
    commandType: CommandType.StoredProcedure,
    parameterSpecs: new[]
    {
        new CommandDefinitionFactory.DbParameterSpec("customerId", DbDataType.Int32, ParameterDirection.Input, 42)
    },
    commandTimeoutSeconds: 30);

(DataSet DataSet, IReadOnlyDictionary<string, object?> OutputParameters) dataSetResult =
    await executor.ExecuteDataSetAsync(dataSetCommand);

DataTable customerTable = dataSetResult.DataSet.Tables[0];
DataTable ordersTable = dataSetResult.DataSet.Tables[1];
```

### B) `QueryTablesAsync` (multi-result + output message)

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

var command = CommandDefinitionFactory.Create(
    databaseType: DatabaseType.SqlServer,
    commandText: "dbo.GetCustomerAndOrdersWithStatus",
    commandType: CommandType.StoredProcedure,
    parameterSpecs: new[]
    {
        new CommandDefinitionFactory.DbParameterSpec("customerId", DbDataType.Int32, ParameterDirection.Input, 42),
        new CommandDefinitionFactory.DbParameterSpec("message", DbDataType.String, ParameterDirection.Output, Size: 4000)
    });

(IReadOnlyList<DataTable> Tables, IReadOnlyDictionary<string, object?> OutputParameters) result =
    await executor.QueryTablesAsync(command);

DataTable customerTable = result.Tables[0];
DataTable ordersTable = result.Tables[1];
string? message = (string?)result.OutputParameters["message"];
```

---

## PostgreSQL

### A) `QueryAsync<T>` (list + output message)

PostgreSQL output parameters for rowsets use refcursors. This example returns one refcursor + message.

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

var command = CommandDefinitionFactory.Create(
    commandText: "public.get_customers_with_status",
    commandType: CommandType.StoredProcedure,
    parameterSpecs: new[]
    {
        new CommandDefinitionFactory.DbParameterSpec("min_id", DbDataType.Int32, ParameterDirection.Input, 100),
        new CommandDefinitionFactory.DbParameterSpec("customer_cursor", DbDataType.RefCursor, ParameterDirection.Output),
        new CommandDefinitionFactory.DbParameterSpec("message", DbDataType.String, ParameterDirection.Output, Size: 4000)
    });

(List<Customer> Rows, IReadOnlyDictionary<string, object?> OutputParameters) result =
    await executor.QueryAsync(
        command,
        row => new Customer(
            Id: row.Field<int>("id"),
            Name: row.Field<string>("name") ?? ""));

List<Customer> customers = result.Rows;
string? message = (string?)result.OutputParameters["message"];
```

Example: refcursor + output parameter + list/table/dataset read:

```csharp
var refCursorSpecs = new List<CommandDefinitionFactory.DbParameterSpec>
{
    new("customer_id", DbDataType.Int32, ParameterDirection.Input, 42),
    new("customer_cursor", DbDataType.RefCursor, ParameterDirection.Output),
    new("orders_cursor", DbDataType.RefCursor, ParameterDirection.Output),
    new("message", DbDataType.String, ParameterDirection.Output, Size: 4000)
};

var refCursorCommand = CommandDefinitionFactory.Create(
    databaseType: DatabaseType.PostgreSql,
    commandText: "public.get_customer_and_orders_with_status",
    commandType: CommandType.StoredProcedure,
    parameterSpecs: refCursorSpecs,
    commandTimeoutSeconds: 30);

// A) List<T> (first cursor only)
(List<Customer> Rows, IReadOnlyDictionary<string, object?> OutputParameters) listResult =
    await executor.QueryAsync(
        refCursorCommand,
        row => new Customer(
            Id: row.Field<int>("id"),
            Name: row.Field<string>("name") ?? ""));

// B) Tables (all cursors)
(IReadOnlyList<DataTable> Tables, IReadOnlyDictionary<string, object?> OutputParameters) tablesResult =
    await executor.QueryTablesAsync(refCursorCommand);

// C) DataSet (all cursors)
(DataSet DataSet, IReadOnlyDictionary<string, object?> OutputParameters) dataSetResult =
    await executor.ExecuteDataSetAsync(refCursorCommand);

string? outMessage = (string?)tablesResult.OutputParameters["message"];
```

### B) `QueryTablesAsync` (refcursor multi-result + output message)

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

var command = CommandDefinitionFactory.Create(
    commandText: "public.get_customer_and_orders_with_status",
    commandType: CommandType.StoredProcedure,
    parameterSpecs: new[]
    {
        new CommandDefinitionFactory.DbParameterSpec("customer_id", DbDataType.Int32, ParameterDirection.Input, 42),
        new CommandDefinitionFactory.DbParameterSpec("customer_cursor", DbDataType.RefCursor, ParameterDirection.Output),
        new CommandDefinitionFactory.DbParameterSpec("orders_cursor", DbDataType.RefCursor, ParameterDirection.Output),
        new CommandDefinitionFactory.DbParameterSpec("message", DbDataType.String, ParameterDirection.Output, Size: 4000)
    });

(IReadOnlyList<DataTable> Tables, IReadOnlyDictionary<string, object?> OutputParameters) result =
    await executor.QueryTablesAsync(command);

DataTable customerTable = result.Tables[0];
DataTable ordersTable = result.Tables[1];
string? message = (string?)result.OutputParameters["message"];
```

---

## Oracle

### A) `QueryAsync<T>` (list + output message)

Oracle rowsets commonly use refcursors. This example returns one refcursor + message.

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

var command = CommandDefinitionFactory.Create(
    commandText: "PKG_CUSTOMER.GET_CUSTOMERS_WITH_STATUS",
    commandType: CommandType.StoredProcedure,
    parameterSpecs: new[]
    {
        new CommandDefinitionFactory.DbParameterSpec("p_min_id", DbDataType.Int32, ParameterDirection.Input, 100),
        new CommandDefinitionFactory.DbParameterSpec("p_customers", DbDataType.RefCursor, ParameterDirection.Output),
        new CommandDefinitionFactory.DbParameterSpec("p_message", DbDataType.String, ParameterDirection.Output, Size: 4000)
    });

(List<Customer> Rows, IReadOnlyDictionary<string, object?> OutputParameters) result =
    await executor.QueryAsync(
        command,
        row => new Customer(
            Id: row.Field<int>("Id"),
            Name: row.Field<string>("Name") ?? ""));

List<Customer> customers = result.Rows;
string? message = (string?)result.OutputParameters["p_message"];
```

### B) `QueryTablesAsync` (refcursor multi-result + output message)

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

var command = CommandDefinitionFactory.Create(
    commandText: "PKG_CUSTOMER.GET_CUSTOMER_AND_ORDERS_WITH_STATUS",
    commandType: CommandType.StoredProcedure,
    parameterSpecs: new[]
    {
        new CommandDefinitionFactory.DbParameterSpec("p_customer_id", DbDataType.Int32, ParameterDirection.Input, 42),
        new CommandDefinitionFactory.DbParameterSpec("p_customer", DbDataType.RefCursor, ParameterDirection.Output),
        new CommandDefinitionFactory.DbParameterSpec("p_orders", DbDataType.RefCursor, ParameterDirection.Output),
        new CommandDefinitionFactory.DbParameterSpec("p_message", DbDataType.String, ParameterDirection.Output, Size: 4000)
    });

(IReadOnlyList<DataTable> Tables, IReadOnlyDictionary<string, object?> OutputParameters) result =
    await executor.QueryTablesAsync(command);

DataTable customerTable = result.Tables[0];
DataTable ordersTable = result.Tables[1];
string? message = (string?)result.OutputParameters["p_message"];
```
