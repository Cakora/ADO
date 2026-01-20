using System;
using AdoAsync.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace AdoAsync.DependencyInjection;

/// <summary>DI helpers for registering AdoAsync executors.</summary>
public static class AdoAsyncServiceCollectionExtensions
{
    /// <summary>Default database name for DI registrations.</summary>
    public const string DefaultName = "Default";

    /// <summary>
    /// Registers a named database options entry and the shared <see cref="IDbExecutorFactory"/>.
    /// </summary>
    public static IServiceCollection AddAdoAsync(this IServiceCollection services, string name, DbOptions options)
    {
        Validate.Required(services, nameof(services));
        Validate.Required(options, nameof(options));

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Database name is required.", nameof(name));
        }

        services.AddSingleton(new NamedDbOptions(name.Trim(), options));
        services.AddSingleton<IDbExecutorFactory, DbExecutorFactory>();
        return services;
    }

    /// <summary>
    /// Registers the default database options entry and adds scoped <see cref="IDbExecutor"/> for that default.
    /// </summary>
    public static IServiceCollection AddAdoAsync(this IServiceCollection services, DbOptions options)
    {
        services.AddAdoAsync(DefaultName, options);
        return services.AddAdoAsyncExecutor(DefaultName);
    }

    /// <summary>
    /// Registers a scoped <see cref="IDbExecutor"/> that resolves to the named database via <see cref="IDbExecutorFactory"/>.
    /// </summary>
    public static IServiceCollection AddAdoAsyncExecutor(this IServiceCollection services, string name = DefaultName)
    {
        Validate.Required(services, nameof(services));

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Database name is required.", nameof(name));
        }

        services.AddScoped<IDbExecutor>(sp => sp.GetRequiredService<IDbExecutorFactory>().Create(name.Trim()));
        return services;
    }
}

