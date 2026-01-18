using System;
using System.Threading.Tasks;
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
            // No-op keeps call sites simple while making retries explicitly opt-in.
            return Policy.NoOpAsync();
        }

        var delay = TimeSpan.FromMilliseconds(options.RetryDelayMilliseconds);
        var retryCount = options.RetryCount;

        return Policy
            .Handle<Exception>(isTransient)
            // Fixed delay keeps retry behavior simple and predictable.
            .WaitAndRetryAsync(retryCount, _ => delay);
    }
    #endregion
}
