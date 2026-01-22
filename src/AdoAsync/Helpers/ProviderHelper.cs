using System;
using AdoAsync.Providers.Oracle;
using AdoAsync.Providers.PostgreSql;
using AdoAsync.Providers.SqlServer;
using AdoAsync.Abstractions;

namespace AdoAsync.Helpers;

internal static class ProviderHelper
{
    /// <summary>Resolve provider implementation for the configured database type.</summary>
    /// <param name="databaseType">Target database provider.</param>
    /// <returns>Concrete provider implementation.</returns>
    public static IDbProvider ResolveProvider(DatabaseType databaseType) =>
        databaseType switch
        {
            DatabaseType.SqlServer => new SqlServerProvider(),
            DatabaseType.PostgreSql => new PostgreSqlProvider(),
            DatabaseType.Oracle => new OracleProvider(),
            _ => throw new DatabaseException(ErrorCategory.Unsupported, $"Database type '{databaseType}' is not supported.")
        };

    /// <summary>Translate a provider exception into the shared DbError contract.</summary>
    /// <param name="databaseType">Target database provider.</param>
    /// <param name="exception">Exception to translate.</param>
    /// <returns>Provider-agnostic error.</returns>
    public static DbError MapProviderError(DatabaseType databaseType, Exception exception) =>
        databaseType switch
        {
            DatabaseType.SqlServer => SqlServerExceptionMapper.Map(exception),
            DatabaseType.PostgreSql => PostgreSqlExceptionMapper.Map(exception),
            DatabaseType.Oracle => OracleExceptionMapper.Map(exception),
            _ => DbErrorMapper.Map(exception)
        };
}
