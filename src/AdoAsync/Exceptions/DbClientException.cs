using System;

namespace AdoAsync;

/// <summary>
/// Unified exception that wraps provider errors into a stable, cross-provider shape.
/// </summary>
public sealed class DbClientException : Exception
{
    /// <summary>Structured error payload for client decisions.</summary>
    public DbError Error { get; }

    /// <summary>Creates a new client exception from a structured error.</summary>
    public DbClientException(DbError error, Exception? innerException = null)
        : base(error.MessageKey, innerException)
    {
        Error = error ?? throw new ArgumentNullException(nameof(error));
    }
}
