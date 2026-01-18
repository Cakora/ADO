using System;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;

namespace AdoAsync.Transactions;

/// <summary>
/// Handle wrapper to expose the active transaction and ensure rollback-on-dispose.
/// </summary>
public sealed class TransactionHandle : IAsyncDisposable
{
    #region Fields
    private readonly TransactionManager _manager;
    private readonly DbTransaction _transaction;
    #endregion

    #region Constructors
    internal TransactionHandle(TransactionManager manager, DbTransaction transaction)
    {
        _manager = manager;
        _transaction = transaction;
    }
    #endregion

    #region Public API
    /// <summary>Active transaction instance.</summary>
    // Expose the underlying transaction for provider-specific operations when needed.
    public DbTransaction Transaction => _transaction;

    /// <summary>Commits the transaction.</summary>
    public ValueTask CommitAsync(CancellationToken cancellationToken = default)
        // Delegate to the manager to enforce a single commit/rollback path.
        => _manager.CommitAsync(cancellationToken);

    /// <summary>Rolls back the transaction.</summary>
    public ValueTask RollbackAsync(CancellationToken cancellationToken = default)
        // Delegate to the manager to keep lifecycle state consistent.
        => _manager.RollbackAsync(cancellationToken);

    /// <summary>Disposes the transaction, rolling back if not committed.</summary>
    public ValueTask DisposeAsync()
        // Centralize dispose logic in the manager to keep state changes consistent.
        => _manager.DisposeAsync();
    #endregion
}
