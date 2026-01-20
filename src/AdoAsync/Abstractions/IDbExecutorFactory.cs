using System;

namespace AdoAsync.Abstractions;

/// <summary>
/// Creates <see cref="IDbExecutor"/> instances for one or more configured databases.
/// </summary>
public interface IDbExecutorFactory
{
    /// <summary>Creates an executor for the configured database name.</summary>
    IDbExecutor Create(string name, bool isInUserTransaction = false);

    /// <summary>Creates an executor for the configured database key.</summary>
    IDbExecutor Create<TName>(TName name, bool isInUserTransaction = false) where TName : struct, Enum;

    /// <summary>Creates an executor for the supplied options.</summary>
    IDbExecutor Create(DbOptions options, bool isInUserTransaction = false);
}
