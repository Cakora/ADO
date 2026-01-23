using System.Data;
using AdoAsync;
using AdoAsync.Execution;

#region Configuration
var provider = Environment.GetEnvironmentVariable("DB_PROVIDER");
var connectionString = Environment.GetEnvironmentVariable("DB_CONNECTION_STRING");

if (string.IsNullOrWhiteSpace(provider) || string.IsNullOrWhiteSpace(connectionString))
{
    Console.WriteLine("Set DB_PROVIDER and DB_CONNECTION_STRING to run the demo.");
    Console.WriteLine("DB_PROVIDER values: mssql | postgres | oracle");
    return;
}
#endregion

#region Demo
DatabaseType databaseType = provider.Trim().ToLowerInvariant() switch
{
    "mssql" or "sqlserver" => DatabaseType.SqlServer,
    "postgres" or "postgresql" => DatabaseType.PostgreSql,
    "oracle" => DatabaseType.Oracle,
    _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, "Unknown provider. Use mssql | postgres | oracle."),
};

var options = new DbOptions
{
    DatabaseType = databaseType,
    ConnectionString = connectionString,
    CommandTimeoutSeconds = 30,
    EnableValidation = true,
    EnableRetry = false
};

await using var executor = DbExecutor.Create(options);

var commandText = databaseType switch
{
    DatabaseType.Oracle => "SELECT CURRENT_TIMESTAMP FROM dual",
    _ => "SELECT CURRENT_TIMESTAMP"
};

(DateTime Value, IReadOnlyDictionary<string, object?> OutputParameters) nowResult =
    await executor.ExecuteScalarAsync<DateTime>(new CommandDefinition
    {
        CommandText = commandText,
        CommandType = CommandType.Text
    });

DateTime now = nowResult.Value;

Console.WriteLine($"Database time: {now:O}");
#endregion
