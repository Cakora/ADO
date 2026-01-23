using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using AdoAsync.Transactions;

namespace AdoAsync.Abstractions;

/// <summary>
/// Facade surface for executing database commands (async-only). Streaming is preferred; materialization is explicit. Not thread-safe; honor cancellation on all methods; retries/transactions are explicit.
/// </summary>
public interface IDbExecutor : IAsyncDisposable
{
    #region Members
    /// <summary>
    /// Executes a single SELECT and returns a streaming reader result (SQL Server/PostgreSQL only).
    /// Output parameters are available only after the reader is closed (when declared on <see cref="CommandDefinition.Parameters"/>).
    /// </summary>
    ValueTask<StreamingReaderResult> ExecuteReaderAsync(CommandDefinition command, CancellationToken cancellationToken = default);

    /// <summary>
    /// Streams records from a single SELECT via ReadAsync (SQL Server/PostgreSQL only).
    /// Output parameters are not returned on this path.
    /// </summary>
    IAsyncEnumerable<IDataRecord> StreamAsync(CommandDefinition command, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a scalar command and returns the value converted to <typeparamref name="T"/> plus output parameters (all providers).
    /// </summary>
    /// <remarks>
    /// Output parameters are returned as a dictionary keyed by parameter name with provider prefixes removed
    /// (for example: "@NewId" → "NewId"). When there are no output parameters, an empty dictionary is returned.
    /// </remarks>
    ValueTask<(T Value, IReadOnlyDictionary<string, object?> OutputParameters)> ExecuteScalarAsync<T>(
        CommandDefinition command,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a non-query command (INSERT/UPDATE/DELETE) and returns affected row count plus output parameters (all providers).
    /// </summary>
    /// <remarks>
    /// Output parameters are returned as a dictionary keyed by parameter name with provider prefixes removed
    /// (for example: "@NewId" → "NewId"). When there are no output parameters, an empty dictionary is returned.
    /// </remarks>
    ValueTask<(int RowsAffected, IReadOnlyDictionary<string, object?> OutputParameters)> ExecuteAsync(
        CommandDefinition command,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a single result and returns a buffered <see cref="DataTable"/> via provider DataAdapter (SQL Server/PostgreSQL/Oracle).
    /// Output parameters are returned in the tuple.
    /// </summary>
    ValueTask<(DataTable Table, IReadOnlyDictionary<string, object?> OutputParameters)> QueryTableAsync(
        CommandDefinition command,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes multiple result sets and returns buffered tables via provider DataAdapter
    /// (SQL Server multi-SELECT, PostgreSQL refcursor/multi-SELECT, Oracle refcursor).
    /// Output parameters are returned in the tuple.
    /// </summary>
    ValueTask<(IReadOnlyList<DataTable> Tables, IReadOnlyDictionary<string, object?> OutputParameters)> QueryTablesAsync(
        CommandDefinition command,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a single result and returns a buffered, mapped list from the resulting DataTable.
    /// Output parameters are returned in the tuple.
    /// </summary>
    ValueTask<(List<T> Rows, IReadOnlyDictionary<string, object?> OutputParameters)> QueryAsync<T>(
        CommandDefinition command,
        Func<DataRow, T> map,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes multiple SELECT statements or cursor results and returns a <see cref="DataSet"/> via provider DataAdapter
    /// (SQL Server multi-SELECT, PostgreSQL refcursor/multi-SELECT, Oracle refcursor).
    /// Output parameters are returned in the tuple.
    /// </summary>
    ValueTask<(DataSet DataSet, IReadOnlyDictionary<string, object?> OutputParameters)> ExecuteDataSetAsync(
        CommandDefinition command,
        CancellationToken cancellationToken = default);

    /// <summary>Begins an explicit transaction on the executor connection (rollback-on-dispose unless committed).</summary>
    ValueTask<TransactionHandle> BeginTransactionAsync(CancellationToken cancellationToken = default);

    #endregion
}
