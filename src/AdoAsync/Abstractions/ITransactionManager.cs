using System;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using AdoAsync.Transactions;

namespace AdoAsync.Abstractions;

/// <summary>
/// Explicit transaction contracts (Approach A).
/// </summary>
public interface ITransactionManager : IAsyncDisposable
{
    #region Members
    /// <summary>Begins a transaction on the provided open connection.</summary>
    // Explicit transaction boundary keeps retries/validation outside the transaction scope.
    ValueTask<TransactionHandle> BeginAsync(DbConnection connection, CancellationToken cancellationToken = default);

    /// <summary>Commits the active transaction.</summary>
    ValueTask CommitAsync(CancellationToken cancellationToken = default);

    /// <summary>Rolls back the active transaction.</summary>
    ValueTask RollbackAsync(CancellationToken cancellationToken = default);
    #endregion
}
