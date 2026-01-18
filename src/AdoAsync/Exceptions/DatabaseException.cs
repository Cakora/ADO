using System;

namespace AdoAsync;

/// <summary>
/// Library-specific exception to avoid leaking framework exception types.
/// </summary>
public sealed class DatabaseException : Exception
{
    /// <summary>High-level reason for the failure.</summary>
    public ErrorCategory Kind { get; }

    /// <summary>Creates a new database exception with a category and message.</summary>
    public DatabaseException(ErrorCategory kind, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        // Preserve the category so callers can branch without string matching.
        Kind = kind;
    }
}

/// <summary>Categories for library exceptions.</summary>
public enum ErrorCategory
{
    /// <summary>Validation or input-related failure.</summary>
    Validation,
    /// <summary>Configuration or setup issue.</summary>
    Configuration,
    /// <summary>Unsupported provider or feature.</summary>
    Unsupported,
    /// <summary>Invalid state or lifecycle usage.</summary>
    State,
    /// <summary>Use-after-dispose or invalid lifetime.</summary>
    Disposed
}
