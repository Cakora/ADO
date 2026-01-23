using System.Threading;
using System.Threading.Tasks;
using AdoAsync.Transactions;

namespace AdoAsync.Execution;

public sealed partial class DbExecutor
{
    #region Public API - Transactions
    /// <summary>Begins an explicit transaction on the shared connection (rollback-on-dispose unless committed).</summary>
    public async ValueTask<TransactionHandle> BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        await EnsureReadyAsync(cancellationToken).ConfigureAwait(false);

        if (_activeTransaction is not null)
        {
            throw new DatabaseException(ErrorCategory.State, "A transaction is already active.");
        }

        var transactionManager = new TransactionManager(_connection!);
        var handle = await transactionManager
            .BeginAsync(_connection!, onDispose: ClearActiveTransaction, cancellationToken)
            .ConfigureAwait(false);

        _activeTransaction = handle.Transaction;
        return handle;
    }
    #endregion

    #region Private - Transactions
    private void ClearActiveTransaction() => _activeTransaction = null;
    #endregion
}
