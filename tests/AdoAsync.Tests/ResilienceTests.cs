using System;
using System.Threading.Tasks;
using AdoAsync.Resilience;
using FluentAssertions;
using Polly;
using Xunit;

namespace AdoAsync.Tests;

public class ResilienceTests
{
    #region Tests
    [Fact]
    public async Task RetryPolicy_Disabled_UsesNoOp()
    {
        var options = new DbOptions
        {
            DatabaseType = DatabaseType.SqlServer,
            ConnectionString = "Server=(local)",
            CommandTimeoutSeconds = 30,
            EnableRetry = false
        };

        var policy = RetryPolicyFactory.Create(options, _ => true, isInUserTransaction: false);

        var attempts = 0;
        // No-op policy should never retry.
        await policy.Awaiting(p => p.ExecuteAsync(async () =>
        {
            await Task.Yield();
            attempts++;
            throw new InvalidOperationException();
        })).Should().ThrowAsync<InvalidOperationException>();

        attempts.Should().Be(1);
        policy.Should().BeOfType<Polly.NoOp.AsyncNoOpPolicy>();
    }

    [Fact]
    public async Task RetryPolicy_SkipsWhenInUserTransaction()
    {
        var options = new DbOptions
        {
            DatabaseType = DatabaseType.SqlServer,
            ConnectionString = "Server=(local)",
            CommandTimeoutSeconds = 30,
            EnableRetry = true
        };

        var policy = RetryPolicyFactory.Create(options, _ => true, isInUserTransaction: true);

        var attempts = 0;
        await policy.Awaiting(p => p.ExecuteAsync(async () =>
        {
            await Task.Yield();
            attempts++;
            throw new InvalidOperationException();
        })).Should().ThrowAsync<InvalidOperationException>();

        attempts.Should().Be(1);
        policy.Should().BeOfType<Polly.NoOp.AsyncNoOpPolicy>();
    }

    [Fact]
    public async Task RetryPolicy_RetriesTransientExceptions()
    {
        var options = new DbOptions
        {
            DatabaseType = DatabaseType.SqlServer,
            ConnectionString = "Server=(local)",
            CommandTimeoutSeconds = 30,
            EnableRetry = true,
            RetryCount = 2,
            RetryDelayMilliseconds = 1
        };

        var policy = RetryPolicyFactory.Create(options, ex => ex is InvalidOperationException);

        var attempts = 0;
        await policy.ExecuteAsync(async () =>
        {
            await Task.Yield();
            attempts++;
            if (attempts == 1)
            {
                throw new InvalidOperationException();
            }
        });

        attempts.Should().Be(2);
    }
    #endregion
}
