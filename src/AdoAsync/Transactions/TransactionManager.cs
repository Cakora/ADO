using System;
using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using AdoAsync.Abstractions;

namespace AdoAsync.Transactions;

/// <summary>Explicit transaction manager with rollback-on-dispose semantics.</summary>
public sealed class TransactionManager : ITransactionManager
{
    private readonly DbConnection _connection;
    private DbTransaction? _transaction;
    private bool _committed;

    /// <summary>Creates a transaction manager bound to the provided connection.</summary>
    public TransactionManager(DbConnection connection)
    {
        Validate.Required(connection, nameof(connection));
        _connection = connection;
    }

    /// <summary>Begins a transaction on the provided connection.</summary>
    public ValueTask<TransactionHandle> BeginAsync(DbConnection connection, CancellationToken cancellationToken = default)
        => BeginAsync(connection, onDispose: null, cancellationToken);

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
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        }

        _transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        return new TransactionHandle(this, _transaction, onDispose);
    }

    /// <summary>Commits the active transaction.</summary>
    public async ValueTask CommitAsync(CancellationToken cancellationToken = default)
    {
        var transaction = _transaction ?? throw new DatabaseException(ErrorCategory.State, "No active transaction to commit.");
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        _committed = true;
    }

    /// <summary>Rolls back the active transaction.</summary>
    public async ValueTask RollbackAsync(CancellationToken cancellationToken = default)
    {
        var transaction = _transaction;
        if (transaction is null)
        {
            return;
        }

        await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Disposes the transaction, rolling back if not committed.</summary>
    public async ValueTask DisposeAsync()
    {
        var transaction = _transaction;
        if (transaction is null)
        {
            return;
        }

        // Make DisposeAsync idempotent and allow a manager instance to be reused.
        _transaction = null;

        // Snapshot the commit state before resetting it so we know whether rollback is required.
        var committed = _committed;

        // Reset to allow reuse and to avoid leaking previous commit state into a future transaction.
        _committed = false;

        try
        {
            if (!committed)
            {
                await transaction.RollbackAsync().ConfigureAwait(false);
            }
        }
        finally
        {
            await transaction.DisposeAsync().ConfigureAwait(false);
        }
    }
}
