using System;
using System.Collections.Generic;
namespace AdoAsync;

/// <summary>
/// Maps exceptions to the provider-agnostic DbError contract.
/// Extend with provider-specific translations in provider namespaces.
/// </summary>
public static class DbErrorMapper
{
    #region Public API
    /// <summary>
    /// Creates a provider-mapped error with consistent shape.
    /// </summary>
    /// <remarks>
    /// Internal helper to keep provider mappers small and consistent.
    /// </remarks>
    internal static DbError FromProvider(
        DbErrorType type,
        string code,
        string messageKey,
        IReadOnlyList<string>? messageParameters,
        bool isTransient,
        string? providerDetails)
    {
        return new DbError
        {
            Type = type,
            Code = code,
            MessageKey = messageKey,
            MessageParameters = messageParameters,
            IsTransient = isTransient,
            ProviderDetails = providerDetails
        };
    }

    /// <summary>
    /// Default retry guidance by error type.
    /// </summary>
    /// <remarks>
    /// This is a library-level policy for buffered operations when retries are enabled and no transaction is active.
    /// Providers may override this in rare cases when a specific code is not safely retryable.
    /// </remarks>
    internal static bool IsTransientByType(DbErrorType type) =>
        type is DbErrorType.Timeout
            or DbErrorType.Deadlock
            or DbErrorType.ConnectionFailure
            or DbErrorType.ResourceLimit;

    /// <summary>Returns an unknown error shape for the given exception.</summary>
    public static DbError Unknown(Exception exception)
    {
        Validate.Required(exception, nameof(exception));
        return new DbError
        {
            Type = DbErrorType.Unknown,
            Code = DbErrorCodes.Unknown,
            MessageKey = "errors.unknown",
                MessageParameters = new[] { exception.Message },
                IsTransient = false,
                // Keep provider type name for diagnostics without exposing raw exceptions.
                ProviderDetails = exception.GetType().FullName
            };
    }

    /// <summary>Maps an exception, optionally overriding provider code/transience.</summary>
    public static DbError Map(Exception exception, string? providerCode = null, bool? isTransientOverride = null)
    {
        Validate.Required(exception, nameof(exception));

        if (exception is DatabaseException dbException)
        {
            return dbException.Kind switch
            {
                ErrorCategory.Validation => Validation(dbException.Message),
                ErrorCategory.Configuration => Unknown(dbException) with { MessageKey = "errors.configuration" },
                ErrorCategory.Unsupported => Unknown(dbException) with { MessageKey = "errors.unsupported" },
                ErrorCategory.State => Unknown(dbException) with { MessageKey = "errors.state" },
                ErrorCategory.Disposed => Unknown(dbException) with { MessageKey = "errors.disposed" },
                _ => Unknown(dbException)
            };
        }

        // Treat explicit timeouts separately so callers can decide to retry.
        if (exception is TimeoutException)
        {
            return new DbError
            {
                Type = DbErrorType.Timeout,
                Code = DbErrorCodes.GenericTimeout,
                MessageKey = "errors.timeout",
                MessageParameters = new[] { exception.Message },
                IsTransient = isTransientOverride ?? true,
                ProviderDetails = providerCode ?? exception.GetType().FullName
            };
        }

        if (exception is OperationCanceledException or TaskCanceledException)
        {
            return new DbError
            {
                Type = DbErrorType.Canceled,
                Code = DbErrorCodes.Canceled,
                MessageKey = "errors.canceled",
                MessageParameters = new[] { exception.Message },
                IsTransient = isTransientOverride ?? false,
                ProviderDetails = providerCode ?? exception.GetType().FullName
            };
        }

        // Extend with provider-specific translators in provider namespaces.
        return Unknown(exception);
    }

    /// <summary>Creates a validation error from message/parameters.</summary>
    public static DbError Validation(string message, IEnumerable<string>? parameters = null)
    {
        Validate.Required(message, nameof(message));
        return new DbError
        {
            Type = DbErrorType.ValidationError,
            Code = DbErrorCodes.ValidationFailed,
            MessageKey = "errors.validation",
            MessageParameters = parameters is null ? new[] { message } : new List<string>(parameters),
            IsTransient = false
        };
    }
    #endregion
}
