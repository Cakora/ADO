using AdoAsync.Abstractions;
using AdoAsync.DependencyInjection;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace AdoAsync.Tests;

public sealed class DependencyInjectionExtensionsTests
{
    [Fact]
    public async Task AddAdoAsync_RegistersFactory_AndCreatesExecutorsByName()
    {
        var services = new ServiceCollection();

        services.AddAdoAsync("Main", CreateValidOptions(DatabaseType.SqlServer));
        services.AddAdoAsync("Reporting", CreateValidOptions(DatabaseType.PostgreSql));

        await using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IDbExecutorFactory>();

        await using var main = factory.Create("Main");
        await using var reporting = factory.Create("Reporting");

        main.Should().NotBeNull();
        reporting.Should().NotBeNull();
    }

    [Fact]
    public async Task AddAdoAsync_Default_RegistersScopedExecutor()
    {
        var services = new ServiceCollection();
        services.AddAdoAsync(CreateValidOptions(DatabaseType.SqlServer));

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        var executor = scope.ServiceProvider.GetRequiredService<IDbExecutor>();
        executor.Should().NotBeNull();
    }

    [Fact]
    public async Task Factory_ThrowsForUnknownName()
    {
        var services = new ServiceCollection();
        services.AddAdoAsync("Main", CreateValidOptions(DatabaseType.SqlServer));

        await using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IDbExecutorFactory>();

        factory.Invoking(f => f.Create("Missing"))
            .Should()
            .Throw<KeyNotFoundException>()
            .WithMessage("*Missing*");
    }

    private static DbOptions CreateValidOptions(DatabaseType databaseType)
    {
        var connectionString = databaseType switch
        {
            DatabaseType.SqlServer => "Server=localhost;Database=unused;User Id=unused;Password=unused;TrustServerCertificate=True;",
            DatabaseType.PostgreSql => "Host=localhost;Database=unused;Username=unused;Password=unused",
            DatabaseType.Oracle => "User Id=unused;Password=unused;Data Source=localhost/unused",
            _ => "unused"
        };

        return new DbOptions
        {
            DatabaseType = databaseType,
            ConnectionString = connectionString,
            CommandTimeoutSeconds = 30,
            EnableValidation = true,
            EnableRetry = false
        };
    }
}
