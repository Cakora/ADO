using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;

namespace AdoAsync.Abstractions;

/// <summary>
/// Facade surface for executing database commands (async-only). Streaming is preferred; materialization is explicit. Not thread-safe; honor cancellation on all methods; retries/transactions are explicit.
/// </summary>
public interface IDbExecutor : IAsyncDisposable
{
    #region Members
    /// <summary>Executes a single SELECT and returns a streaming reader (SQL Server/PostgreSQL only; caller owns reader lifecycle). Output parameters are not available on this streaming path.</summary>
    ValueTask<DbDataReader> ExecuteReaderAsync(CommandDefinition command, CancellationToken cancellationToken = default);

    /// <summary>Executes a single SELECT and returns a streaming reader plus deferred output parameters (SQL Server/PostgreSQL only).</summary>
    ValueTask<StreamingReaderResult> ExecuteReaderWithOutputsAsync(CommandDefinition command, CancellationToken cancellationToken = default);

    /// <summary>Streams records from a single SELECT via ReadAsync (SQL Server/PostgreSQL only). Output parameters are not available on this streaming path.</summary>
    IAsyncEnumerable<IDataRecord> StreamAsync(CommandDefinition command, CancellationToken cancellationToken = default);

    /// <summary>Executes a scalar command and returns the value converted to <typeparamref name="T"/> (all providers). Output parameters are not returned on this scalar path.</summary>
    ValueTask<T> ExecuteScalarAsync<T>(CommandDefinition command, CancellationToken cancellationToken = default);

    /// <summary>Executes a non-query command (INSERT/UPDATE/DELETE) and returns the affected row count (all providers).</summary>
    /// <remarks>
    /// Use this for commands where the database returns rows-affected via ExecuteNonQuery.
    /// Output parameters are not returned by this method. If you need output parameters, use a buffered method
    /// such as <see cref="QueryTableAsync"/> / <see cref="ExecuteDataSetAsync"/> (and read outputs from ExtendedProperties),
    /// or use <see cref="ExecuteReaderWithOutputsAsync"/> for streaming (SQL Server/PostgreSQL only).
    /// </remarks>
    ValueTask<int> ExecuteAsync(CommandDefinition command, CancellationToken cancellationToken = default);

    /// <summary>Executes a single result and returns a buffered <see cref="DataTable"/> via provider DataAdapter (SQL Server/PostgreSQL/Oracle). Output parameters, when present, are attached to DataTable.ExtendedProperties["OutputParameters"].</summary>
    ValueTask<DataTable> QueryTableAsync(CommandDefinition command, CancellationToken cancellationToken = default);

    /// <summary>Executes a single result and returns a buffered, mapped list from the resulting DataTable. Output parameters are available on the underlying DataTable ExtendedProperties.</summary>
    ValueTask<List<T>> QueryAsync<T>(CommandDefinition command, Func<DataRow, T> map, CancellationToken cancellationToken = default);

    /// <summary>Executes multiple SELECT statements or cursor results and returns a <see cref="DataSet"/> via provider DataAdapter (SQL Server multi-SELECT, PostgreSQL refcursor/multi-SELECT, Oracle refcursor). Output parameters, when present, are attached to DataSet.ExtendedProperties["OutputParameters"].</summary>
    ValueTask<DataSet> ExecuteDataSetAsync(CommandDefinition command, CancellationToken cancellationToken = default);

    #endregion
}
