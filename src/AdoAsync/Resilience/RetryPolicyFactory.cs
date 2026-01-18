using System;
using System.Threading.Tasks;
using AdoAsync;
using Polly;

namespace AdoAsync.Resilience;

/// <summary>
/// Builds Polly retry policies based on DbOptions. No hidden retries.
/// </summary>
public static class RetryPolicyFactory
{
    #region Public API
    /// <summary>Creates a retry policy from options and transient predicate; disabled for user transactions.</summary>
    public static IAsyncPolicy Create(
        DbOptions options,
        Func<Exception, bool> isTransient,
        bool isInUserTransaction = false)
    {
        Validate.Required(options, nameof(options));
        Validate.Required(isTransient, nameof(isTransient));

        // Avoid retries inside caller-managed transactions to prevent partial work repeats.
        if (!options.EnableRetry || isInUserTransaction)
        {
            // NoOp keeps the call sites uniform even when retries are disabled.
            return Policy.NoOpAsync();
        }

        var delay = TimeSpan.FromMilliseconds(options.RetryDelayMilliseconds);
        var retryCount = options.RetryCount;

        return Policy
            // The transient predicate keeps provider-specific logic out of the resilience layer.
            .Handle<Exception>(isTransient)
            // Fixed backoff keeps retry behavior predictable and easy to reason about.
            .WaitAndRetryAsync(retryCount, _ => delay);
    }
    #endregion
}
