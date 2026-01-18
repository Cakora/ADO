using System;
using System.Collections.Generic;
using System.Data;
using System.Threading;
using System.Threading.Tasks;

namespace AdoAsync.Abstractions;

/// <summary>
/// Facade surface for executing database commands (async-only). Streaming is preferred; materialization is explicit. Not thread-safe; honor cancellation on all methods; retries/transactions are explicit.
/// </summary>
public interface IDbExecutor : IAsyncDisposable
{
    #region Members
    /// <summary>Executes a non-query command.</summary>
    // Non-query is separated to keep result shape small and predictable.
    ValueTask<int> ExecuteAsync(CommandDefinition command, CancellationToken cancellationToken = default);

    /// <summary>Executes a scalar command and returns the value converted to <typeparamref name="T"/>.</summary>
    // Conversion happens in the executor to keep providers focused on ADO.NET bindings.
    ValueTask<T> ExecuteScalarAsync<T>(CommandDefinition command, CancellationToken cancellationToken = default);

    /// <summary>Streams rows and maps each record via the provided mapper.</summary>
    // Streaming keeps memory usage predictable for large result sets.
    IAsyncEnumerable<T> QueryAsync<T>(CommandDefinition command, Func<IDataRecord, T> map, CancellationToken cancellationToken = default);

    /// <summary>Executes a command and materializes results into tables (allocation-heavy).</summary>
    // Kept separate from streaming APIs to make allocation cost explicit.
    ValueTask<DbResult> QueryTablesAsync(CommandDefinition command, CancellationToken cancellationToken = default);

    /// <summary>Performs a bulk import into a destination table.</summary>
    // Bulk import is provider-specific but exposed uniformly for callers.
    ValueTask<BulkImportResult> BulkImportAsync(BulkImportRequest request, CancellationToken cancellationToken = default);
    #endregion
}
