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
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(isTransient);

        // Avoid retries inside caller-managed transactions to prevent partial work repeats.
        if (!options.EnableRetry || isInUserTransaction)
        {
            return Policy.NoOpAsync();
        }

        var delay = TimeSpan.FromMilliseconds(options.RetryDelayMilliseconds);
        var retryCount = options.RetryCount;

        return Policy
            .Handle<Exception>(isTransient)
            .WaitAndRetryAsync(retryCount, _ => delay);
    }
    #endregion
}
