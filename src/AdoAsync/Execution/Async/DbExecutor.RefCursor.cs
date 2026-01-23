using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using AdoAsync.Helpers;
using AdoAsync.Providers.Oracle;
using AdoAsync.Providers.PostgreSql;

namespace AdoAsync.Execution;

/// <summary>Refcursor-specific paths (extracted for readability).</summary>
public sealed partial class DbExecutor
{
    #region Refcursor - Routing
    private bool ShouldUseOracleRefCursorPath(CommandDefinition command) =>
        CursorHelper.IsOracleRefCursor(_options.DatabaseType, command);

    private bool ShouldUsePostgresRefCursorPath(CommandDefinition command) =>
        CursorHelper.IsPostgresRefCursor(_options.DatabaseType, command);

    private bool IsRefCursorCommand(CommandDefinition command) =>
        ShouldUseOracleRefCursorPath(command) || ShouldUsePostgresRefCursorPath(command);

    private ValueTask<(IReadOnlyList<DataTable> Tables, IReadOnlyDictionary<string, object?> OutputParameters)> ExecuteRefCursorsWithOutputsAsync(
        CommandDefinition command,
        CancellationToken cancellationToken) =>
        ShouldUseOracleRefCursorPath(command)
            ? ExecuteOracleRefCursorsWithOutputsAsync(command, cancellationToken)
            : ExecutePostgresRefCursorsWithOutputsAsync(command, cancellationToken);
    #endregion

    #region Refcursor - Transaction Scope
    private async ValueTask<T> WithTransactionScopeAsync<T>(CancellationToken cancellationToken, Func<DbTransaction, Task<T>> action)
    {
        // PostgreSQL refcursors must be fetched within the same transaction that created them.
        // Reuse an existing user transaction when present; otherwise create a local transaction.
        await EnsureReadyAsync(cancellationToken).ConfigureAwait(false);

        if (_activeTransaction is not null)
        {
            return await action(_activeTransaction).ConfigureAwait(false);
        }

        await using var transaction = await _connection!.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var result = await action(transaction).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return result;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }
    #endregion

    #region Refcursor - Oracle
    // Oracle exposes result sets via output refcursor parameters; reading is handled provider-side.
    private async ValueTask<(IReadOnlyList<DataTable> Tables, IReadOnlyDictionary<string, object?> OutputParameters)> ExecuteOracleRefCursorsWithOutputsAsync(
        CommandDefinition command,
        CancellationToken cancellationToken)
    {
        try
        {
            // Policy: do not retry refcursor stored procedures.
            // Even when the provider doesn't require an explicit transaction (Oracle),
            // stored procedures can have side effects and retries can duplicate work.
            await EnsureNotDisposedAsync().ConfigureAwait(false);
            await using var dbCommand = await CreateCommandAsync(command, cancellationToken).ConfigureAwait(false);
            await dbCommand.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

            var tables = OracleProvider.ReadRefCursorResults(dbCommand);
            var outputs = ExtractOutputParametersOrEmpty(dbCommand, command.Parameters);
            return (Tables: tables, OutputParameters: outputs);
        }
        catch (Exception ex)
        {
            var error = MapError(ex);
            throw new DbCallerException(error, ex);
        }
    }
    #endregion

    #region Refcursor - PostgreSQL
    // PostgreSQL returns refcursor names as output parameters; results must be fetched explicitly (FETCH ALL IN ...)
    // and must run inside the same transaction scope.
    private async ValueTask<(IReadOnlyList<DataTable> Tables, IReadOnlyDictionary<string, object?> OutputParameters)> ExecutePostgresRefCursorsWithOutputsAsync(
        CommandDefinition command,
        CancellationToken cancellationToken)
    {
        try
        {
            // Policy: never retry when a transaction scope is required.
            // PostgreSQL refcursors require a transaction; retry would re-run the stored procedure and can duplicate side effects.
            return await ExecutePostgresRefCursorsWithOutputsCoreAsync(command, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            var error = MapError(ex);
            throw new DbCallerException(error, ex);
        }
    }

    private async Task<(IReadOnlyList<DataTable> Tables, IReadOnlyDictionary<string, object?> OutputParameters)> ExecutePostgresRefCursorsWithOutputsCoreAsync(
        CommandDefinition command,
        CancellationToken cancellationToken)
    {
        // Always ensure a transaction for opening + fetching the cursors.
        return await WithTransactionScopeAsync(
                cancellationToken,
                tx => ExecutePostgresRefCursorsInTransactionAsync(command, tx, cancellationToken))
            .ConfigureAwait(false);
    }

    private async Task<(IReadOnlyList<DataTable> Tables, IReadOnlyDictionary<string, object?> OutputParameters)> ExecutePostgresRefCursorsInTransactionAsync(
        CommandDefinition command,
        DbTransaction transaction,
        CancellationToken cancellationToken)
    {
        await using var dbCommand = await CreateCommandAsync(command, cancellationToken).ConfigureAwait(false);
        dbCommand.Transaction = transaction;
        await dbCommand.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

        var tables = await PostgreSqlProvider
            .ReadRefCursorResultsAsync(dbCommand, _connection!, transaction, cancellationToken)
            .ConfigureAwait(false);

        var outputs = ExtractOutputParametersOrEmpty(dbCommand, command.Parameters);
        return (Tables: tables, OutputParameters: outputs);
    }
    #endregion
}
