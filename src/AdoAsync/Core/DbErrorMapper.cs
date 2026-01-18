using System;
using System.Collections.Generic;
using System.Threading;

namespace AdoAsync;

/// <summary>
/// Maps exceptions to the provider-agnostic DbError contract.
/// Extend with provider-specific translations in provider namespaces.
/// </summary>
public static class DbErrorMapper
{
    #region Public API
    /// <summary>Returns an unknown error shape for the given exception.</summary>
    public static DbError Unknown(Exception exception)
    {
        Validate.Required(exception, nameof(exception));
        // Keep the unknown shape stable to avoid leaking provider-specific types.
        return new DbError
        {
            Type = DbErrorType.Unknown,
            Code = DbErrorCode.Unknown,
            MessageKey = "errors.unknown",
            MessageParameters = new[] { exception.Message },
            IsTransient = false,
            ProviderDetails = exception.GetType().FullName
        };
    }

    /// <summary>Maps an exception, optionally overriding provider code/transience.</summary>
    public static DbError Map(Exception exception, string? providerCode = null, bool? isTransientOverride = null)
    {
        Validate.Required(exception, nameof(exception));

        if (exception is TimeoutException)
        {
            return new DbError
            {
                Type = DbErrorType.Timeout,
                Code = DbErrorCode.GenericTimeout,
                MessageKey = "errors.timeout",
                MessageParameters = new[] { exception.Message },
                // Default to transient for timeouts unless a provider overrides.
                IsTransient = isTransientOverride ?? true,
                ProviderDetails = providerCode ?? exception.GetType().FullName
            };
        }

        if (exception is OperationCanceledException or TaskCanceledException)
        {
            return new DbError
            {
                Type = DbErrorType.Timeout,
                Code = DbErrorCode.GenericTimeout,
                MessageKey = "errors.canceled",
                MessageParameters = new[] { exception.Message },
                // Cancellations are usually caller-driven, so don't mark as transient by default.
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
            Code = DbErrorCode.ValidationFailed,
            MessageKey = "errors.validation",
            // MessageParameters carries per-field context for client display.
            MessageParameters = parameters is null ? new[] { message } : new List<string>(parameters),
            // Validation errors are deterministic, not transient.
            IsTransient = false
        };
    }
    #endregion
}
