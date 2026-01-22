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
    /// <summary>Executes a single SELECT and returns a streaming reader (SQL Server/PostgreSQL only; caller owns reader lifecycle).</summary>
    ValueTask<DbDataReader> ExecuteReaderAsync(CommandDefinition command, CancellationToken cancellationToken = default);

    /// <summary>Streams records from a single SELECT via ReadAsync (SQL Server/PostgreSQL only).</summary>
    IAsyncEnumerable<IDataRecord> StreamAsync(CommandDefinition command, CancellationToken cancellationToken = default);

    /// <summary>Executes a scalar command and returns the value converted to <typeparamref name="T"/>.</summary>
    ValueTask<T> ExecuteScalarAsync<T>(CommandDefinition command, CancellationToken cancellationToken = default);

    /// <summary>Executes a single result and returns a buffered <see cref="DataTable"/> via provider DataAdapter.</summary>
    ValueTask<DataTable> QueryTableAsync(CommandDefinition command, CancellationToken cancellationToken = default);

    /// <summary>Executes a single result and returns a buffered, mapped list from the resulting DataTable.</summary>
    ValueTask<List<T>> QueryAsync<T>(CommandDefinition command, Func<DataRow, T> map, CancellationToken cancellationToken = default);

    /// <summary>Executes multiple SELECT statements or cursor results and returns buffered tables (includes output parameters).</summary>
    ValueTask<DbResult> QueryTablesAsync(CommandDefinition command, CancellationToken cancellationToken = default);

    /// <summary>Executes multiple SELECT statements or cursor results and returns a <see cref="DataSet"/> via provider DataAdapter.</summary>
    ValueTask<DataSet> ExecuteDataSetAsync(CommandDefinition command, CancellationToken cancellationToken = default);

    /// <summary>Executes multiple SELECT statements or cursor results and returns a structured multi-result (DataSet + output parameters).</summary>
    ValueTask<MultiResult> QueryMultipleAsync(CommandDefinition command, CancellationToken cancellationToken = default);
    #endregion
}
