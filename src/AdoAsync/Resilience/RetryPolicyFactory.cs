using System;
using System.Threading.Tasks;
using Polly;

namespace AdoAsync.Resilience;

/// <summary>
/// Builds Polly retry policies based on DbOptions. No hidden retries.
///
/// Retries are attempted only when:
/// - <see cref="DbOptions.EnableRetry"/> is true, AND
/// - the caller is not inside an explicit user transaction, AND
/// - the provider exception mapper marks the error as transient (<c>DbError.IsTransient == true</c>).
///
/// Transient errors currently include (provider-specific):
/// - Timeouts (e.g., SQL Server timeout, PostgreSQL query canceled, Oracle ORA-01013/ORA-12170)
/// - Deadlocks/serialization failures
/// - Connection failures
/// - Some resource throttling cases (SQL Server 10928/10929; PostgreSQL lock not available)
///
/// Non-transient errors (e.g., missing tables/procedures, syntax errors, validation errors) are not retried.
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
