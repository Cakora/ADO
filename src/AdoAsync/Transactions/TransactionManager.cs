using System;
using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using AdoAsync.Abstractions;

namespace AdoAsync.Transactions;

/// <summary>
/// Explicit transaction manager (Approach A) with rollback-on-dispose semantics.
/// </summary>
public sealed class TransactionManager : ITransactionManager
{
    #region Fields
    private readonly DbConnection _connection;
    private DbTransaction? _transaction;
    private bool _committed;
    #endregion

    #region Constructors
    /// <summary>Creates a transaction manager bound to the provided connection.</summary>
    public TransactionManager(DbConnection connection)
    {
        Validate.Required(connection, nameof(connection));
        _connection = connection!;
    }
    #endregion

    #region Public API
    /// <summary>Begins a transaction on the provided connection.</summary>
    public async ValueTask<TransactionHandle> BeginAsync(DbConnection connection, CancellationToken cancellationToken = default)
        => await BeginAsync(connection, onDispose: null, cancellationToken).ConfigureAwait(false);

    internal async ValueTask<TransactionHandle> BeginAsync(
        DbConnection connection,
        Action? onDispose,
        CancellationToken cancellationToken = default)
    {
        if (!ReferenceEquals(connection, _connection))
        {
            throw new DatabaseException(ErrorCategory.State, "TransactionManager must use the same connection instance.");
        }

        if (_transaction is not null)
        {
            throw new DatabaseException(ErrorCategory.State, "Transaction already started.");
        }

        if (connection.State != ConnectionState.Open)
        {
            // Ensure the transaction is bound to an open connection.
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        }

        _transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        return new TransactionHandle(this, _transaction, onDispose);
    }

    /// <summary>Commits the active transaction.</summary>
    public async ValueTask CommitAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction is null)
        {
            throw new DatabaseException(ErrorCategory.State, "No active transaction to commit.");
        }

        await _transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        _committed = true;
    }

    /// <summary>Rolls back the active transaction.</summary>
    public async ValueTask RollbackAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction is null)
        {
            return;
        }

        await _transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Disposes the transaction, rolling back if not committed.</summary>
    public async ValueTask DisposeAsync()
    {
        if (_transaction is not null)
        {
            if (!_committed)
            {
                // Safety net: avoid leaving open transactions when callers forget to commit.
                await _transaction.RollbackAsync().ConfigureAwait(false);
            }

            await _transaction.DisposeAsync().ConfigureAwait(false);
        }
    }
    #endregion
}
