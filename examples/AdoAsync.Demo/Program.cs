using AdoAsync;

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
DbProvider dbProvider = provider.Trim().ToLowerInvariant() switch
{
    "mssql" or "sqlserver" => DbProvider.SqlServer,
    "postgres" or "postgresql" => DbProvider.PostgreSql,
    "oracle" => DbProvider.Oracle,
    _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, "Unknown provider. Use mssql | postgres | oracle."),
};

var client = new AdoAsyncClient(connectionString, dbProvider);

var now = await client.ExecuteScalarAsync<DateTime>(
    new CommandDefinition("SELECT CURRENT_TIMESTAMP"));

Console.WriteLine($"Database time: {now:O}");
#endregion
