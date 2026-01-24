using System;
using System.Collections.Generic;
using System.Data;
using System.Runtime.CompilerServices;
using System.Threading;
using AdoAsync.Abstractions;

namespace AdoAsync.Execution;

/// <summary>Convenience helpers built on top of <see cref="IDbExecutor"/>.</summary>
public static class DbExecutorQueryExtensions
{
    /// <summary>
    /// Streams a single SELECT and maps each row to <typeparamref name="T"/> (SQL Server/PostgreSQL only).
    /// This is a thin wrapper over <see cref="IDbExecutor.StreamAsync"/>.
    /// </summary>
    public static IAsyncEnumerable<T> QueryAsync<T>(
        this IDbExecutor executor,
        CommandDefinition command,
        Func<IDataRecord, T> map,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(executor);
        ArgumentNullException.ThrowIfNull(map);

        return QueryAsyncIterator(executor, command, map, cancellationToken);
    }

    private static async IAsyncEnumerable<T> QueryAsyncIterator<T>(
        IDbExecutor executor,
        CommandDefinition command,
        Func<IDataRecord, T> map,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var record in executor.StreamAsync(command, cancellationToken).ConfigureAwait(false))
        {
            yield return map(record);
        }
    }
}

