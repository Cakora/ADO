using System.Threading;
using System.Threading.Tasks;
using AdoAsync.Abstractions;

namespace AdoAsync;

/// <summary>SQL Server convenience wrapper for per-call connection strings.</summary>
public static class SqlServerAdo
{
    private static readonly DbOptions BaseOptions = new()
    {
        DatabaseType = DatabaseType.SqlServer,
        ConnectionString = "",
        CommandTimeoutSeconds = 30,
        EnableValidation = true,
        EnableRetry = false
    };

    /// <summary>
    /// Creates a new executor for the provided connection string and executes the command.
    /// </summary>
    /// <param name="factory">Executor factory (register via <c>AddAdoAsyncFactory()</c>).</param>
    /// <param name="connectionString">SQL Server connection string.</param>
    /// <param name="command">Command definition to execute.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static async Task<int> ExecuteAsync(
        IDbExecutorFactory factory,
        string connectionString,
        CommandDefinition command,
        CancellationToken cancellationToken = default)
    {
        var options = BaseOptions with { ConnectionString = connectionString };
        await using var executor = factory.Create(options);
        return await executor.ExecuteAsync(command, cancellationToken).ConfigureAwait(false);
    }
}
