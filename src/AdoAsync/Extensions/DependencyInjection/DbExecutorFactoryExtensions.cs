using System;
using AdoAsync.Abstractions;

namespace AdoAsync.DependencyInjection;

/// <summary>Convenience extensions for enum-keyed multi-database setups.</summary>
public static class DbExecutorFactoryExtensions
{
    /// <summary>Creates an executor for the configured enum database key.</summary>
    public static IDbExecutor Create<TName>(this IDbExecutorFactory factory, TName name, bool isInUserTransaction = false)
        where TName : struct, Enum
    {
        Validate.Required(factory, nameof(factory));
        return factory.Create(name.ToString(), isInUserTransaction);
    }
}

