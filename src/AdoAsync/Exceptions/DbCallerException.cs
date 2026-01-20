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

    /// <summary>Creates a new caller exception from a structured error.</summary>
    public DbCallerException(DbError error, Exception? innerException = null)
        : base((error ?? throw new ArgumentNullException(nameof(error))).MessageKey, innerException)
    {
        Error = error;
    }
}
