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
    private readonly Action? _onDispose;
    #endregion

    #region Constructors
    internal TransactionHandle(TransactionManager manager, DbTransaction transaction, Action? onDispose = null)
    {
        _manager = manager;
        _transaction = transaction;
        _onDispose = onDispose;
    }
    #endregion

    #region Public API
    /// <summary>Active transaction instance.</summary>
    // Expose for provider-specific operations when needed.
    public DbTransaction Transaction => _transaction;

    /// <summary>Commits the transaction.</summary>
    public ValueTask CommitAsync(CancellationToken cancellationToken = default)
        => _manager.CommitAsync(cancellationToken);

    /// <summary>Rolls back the transaction.</summary>
    public ValueTask RollbackAsync(CancellationToken cancellationToken = default)
        => _manager.RollbackAsync(cancellationToken);

    /// <summary>Disposes the transaction, rolling back if not committed.</summary>
    public async ValueTask DisposeAsync()
    {
        try
        {
            await _manager.DisposeAsync().ConfigureAwait(false);
        }
        finally
        {
            _onDispose?.Invoke();
        }
    }
    #endregion
}
