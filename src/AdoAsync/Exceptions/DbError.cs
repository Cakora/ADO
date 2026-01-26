using System.Collections.Generic;

namespace AdoAsync;

/// <summary>
/// Provider-agnostic error contract suitable for middleware and localization.
/// </summary>
public sealed record DbError
{
    #region Members
    /// <summary>Error category.</summary>
    public required DbErrorType Type { get; init; }

    /// <summary>Stable error code.</summary>
    public required string Code { get; init; }

    /// <summary>Stable key for localization lookup.</summary>
    // Keep message keys stable so UI can translate without parsing errors.
    public string MessageKey { get; init; } = string.Empty;

    /// <summary>Optional parameters for localization formatting.</summary>
    public IReadOnlyList<string>? MessageParameters { get; init; }

    /// <summary>Optional localized message (if available).</summary>
    public string? LocalizedMessage { get; init; }

    /// <summary>Indicates whether the error is considered transient (safe to retry/backoff).</summary>
    public bool IsTransient { get; init; }

    /// <summary>Optional provider-specific details for diagnostics (never raw exceptions).</summary>
    // Keep this string-only to avoid leaking exception objects across layers.
    public string? ProviderDetails { get; init; }
    #endregion
}

/// <summary>Top-level error categories.</summary>
public enum DbErrorType
{
    #region Values
    /// <summary>Operation was canceled (typically via CancellationToken).</summary>
    Canceled,
    /// <summary>Execution timed out.</summary>
    Timeout,
    /// <summary>Deadlock detected.</summary>
    Deadlock,
    /// <summary>Connection failure.</summary>
    ConnectionFailure,
    /// <summary>Resource limit reached.</summary>
    ResourceLimit,
    /// <summary>Validation failure.</summary>
    ValidationError,
    /// <summary>SQL or command syntax error.</summary>
    SyntaxError,
    /// <summary>Unclassified error.</summary>
    Unknown
    #endregion
}

/// <summary>Stable error codes as string constants.</summary>
public static class DbErrorCodes
{
    /// <summary>Authentication failed (invalid login / credentials).</summary>
    public const string AuthenticationFailed = "authentication_failed";

    /// <summary>Operation was canceled (typically via CancellationToken).</summary>
    public const string Canceled = "canceled";

    /// <summary>Generic timeout code (provider-agnostic).</summary>
    public const string GenericTimeout = "timeout";

    /// <summary>Generic deadlock code (provider-agnostic).</summary>
    public const string GenericDeadlock = "deadlock";

    /// <summary>Connection was lost or dropped during execution.</summary>
    public const string ConnectionLost = "connection_lost";

    /// <summary>Resource limit exceeded (e.g., quota, pool limit, memory/space pressure).</summary>
    public const string ResourceLimitExceeded = "resource_limit_exceeded";

    /// <summary>Validation failed (caller input / command definition).</summary>
    public const string ValidationFailed = "validation_failed";

    /// <summary>SQL or command syntax error.</summary>
    public const string SyntaxError = "syntax_error";

    /// <summary>Fallback for unmapped provider errors.</summary>
    public const string Unknown = "unknown";
}
