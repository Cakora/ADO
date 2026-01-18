using System.Data.Common;

namespace AdoAsync;

/// <summary>
/// Provider-agnostic database options. Contains no logic or environment reads.
/// </summary>
public sealed record DbOptions
{
    #region Members
    /// <summary>Target database provider.</summary>
    public required DatabaseType DatabaseType { get; init; }

    /// <summary>Connection string for the chosen provider.</summary>
    public required string ConnectionString { get; init; }

    /// <summary>Command timeout in seconds.</summary>
    public required int CommandTimeoutSeconds { get; init; }

    /// <summary>Enables diagnostics collection.</summary>
    // Keeps instrumentation opt-in to avoid overhead in hot paths.
    public bool EnableDiagnostics { get; init; } = false;

    /// <summary>Enables FluentValidation checks.</summary>
    // Validation is opt-out to catch mistakes early in development.
    public bool EnableValidation { get; init; } = true;

    /// <summary>Enables Polly-backed retries.</summary>
    // Retries are explicit and off by default to avoid hidden behavior.
    public bool EnableRetry { get; init; } = false;

    /// <summary>Retry count when retries are enabled.</summary>
    public int RetryCount { get; init; } = 3;

    /// <summary>Delay (milliseconds) between retries when enabled.</summary>
    public int RetryDelayMilliseconds { get; init; } = 200;

    /// <summary>
    /// Optional provider data source; when set, connections are created from this data source instead of the raw connection string.
    /// </summary>
    // DataSource supports centralized pooling/configuration without changing the public API surface.
    public DbDataSource? DataSource { get; init; }
    #endregion
}

/// <summary>Supported database providers.</summary>
public enum DatabaseType
{
    #region Values
    /// <summary>Microsoft SQL Server.</summary>
    SqlServer,
    /// <summary>PostgreSQL.</summary>
    PostgreSql,
    /// <summary>Oracle Database.</summary>
    Oracle
    #endregion
}
