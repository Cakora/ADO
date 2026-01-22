using System;
using AdoAsync.Abstractions;
using AdoAsync.Helpers;

namespace AdoAsync.Exceptions;

/// <summary>
/// Centralized exception mapping and wrapping for provider and caller exceptions.
/// </summary>
public static class ExceptionHandler
{
    /// <summary>Map a provider exception to a standardized DbError.</summary>
    public static DbError Map(DatabaseType databaseType, Exception exception)
    {
        // Reuse existing provider-specific mappers via ProviderHelper.
        return ProviderHelper.MapProviderError(databaseType, exception);
    }

    /// <summary>Wrap an exception in a DbCallerException using mapped DbError.</summary>
    public static DbCallerException Wrap(DatabaseType databaseType, Exception exception)
    {
        var error = Map(databaseType, exception);
        return new DbCallerException(error, exception);
    }
}
