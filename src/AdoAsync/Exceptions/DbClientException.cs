using System;

namespace AdoAsync;

/// <summary>
/// Legacy name for <see cref="DbCallerException"/>. Prefer <see cref="DbCallerException"/> in new code.
/// </summary>
public sealed class DbClientException : DbCallerException
{
    /// <summary>Creates a new exception from a structured error.</summary>
    public DbClientException(DbError error, Exception? innerException = null)
        : base(error, innerException)
    {
    }
}
