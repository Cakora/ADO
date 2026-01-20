using System;

namespace AdoAsync;

/// <summary>
/// Unified exception intended for library callers (your application code).
/// </summary>
/// <remarks>
/// Use <see cref="DbCallerException"/> to catch AdoAsync failures without depending on provider/framework exception types.
/// <see cref="DatabaseException"/> is used internally and typically gets mapped to a <see cref="DbError"/> instead.
/// </remarks>
public class DbCallerException : Exception
{
    /// <summary>Structured error payload for caller decisions.</summary>
    public DbError Error { get; }

    /// <summary>Convenience accessor for <see cref="DbError.Type"/>.</summary>
    public DbErrorType ErrorType => Error.Type;

    /// <summary>Convenience accessor for <see cref="DbError.Code"/>.</summary>
    public DbErrorCode ErrorCode => Error.Code;

    /// <summary>Indicates whether the error is marked transient by the provider mapper.</summary>
    public bool IsTransient => Error.IsTransient;

    /// <summary>Stable localization/message key without parsing the exception message.</summary>
    public string MessageKey => Error.MessageKey;

    /// <summary>Optional provider diagnostics if supplied by the mapper.</summary>
    public string? ProviderDetails => Error.ProviderDetails;

    /// <summary>Creates a new caller exception from a structured error.</summary>
    public DbCallerException(DbError error, Exception? innerException = null)
        : base((error ?? throw new ArgumentNullException(nameof(error))).MessageKey, innerException)
    {
        Error = error;
    }
}
