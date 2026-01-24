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
    public required DbErrorCode Code { get; init; }

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

/// <summary>Stable error codes.</summary>
public enum DbErrorCode
{
    #region Values
    /// <summary>Authentication/authorization failure (invalid credentials or login rejected).</summary>
    AuthenticationFailed,
    /// <summary>Operation was canceled (typically via CancellationToken).</summary>
    Canceled,
    /// <summary>Timeout occurred.</summary>
    GenericTimeout,
    /// <summary>Deadlock occurred.</summary>
    GenericDeadlock,
    /// <summary>Connection lost.</summary>
    ConnectionLost,
    /// <summary>Resource limit exceeded.</summary>
    ResourceLimitExceeded,
    /// <summary>Validation failed.</summary>
    ValidationFailed,
    /// <summary>Syntax error.</summary>
    SyntaxError,
    /// <summary>Unclassified error.</summary>
    Unknown
    #endregion
}
