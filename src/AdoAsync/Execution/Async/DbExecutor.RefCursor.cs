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
    #region Refcursor routing
    private bool ShouldUseOracleRefCursorPath(CommandDefinition command) =>
        CursorHelper.IsOracleRefCursor(_options.DatabaseType, command);

    private bool ShouldUsePostgresRefCursorPath(CommandDefinition command) =>
        CursorHelper.IsPostgresRefCursor(_options.DatabaseType, command);
    #endregion

    #region Refcursor transaction helpers
    private async ValueTask<T> WithTransactionScopeAsync<T>(CancellationToken cancellationToken, Func<DbTransaction, Task<T>> action)
    {
        // Refcursors must be consumed within a transaction scope (reuse caller transaction when present).
        await EnsureConnectionAsync(cancellationToken).ConfigureAwait(false);

        await using var transaction = _activeTransaction is null
            ? await _connection!.BeginTransactionAsync(cancellationToken).ConfigureAwait(false)
            : null;
        var startedTransaction = transaction is not null;
        var effectiveTransaction = transaction ?? _activeTransaction!;

        try
        {
            var result = await action(effectiveTransaction).ConfigureAwait(false);
            if (startedTransaction)
            {
                await transaction!.CommitAsync(cancellationToken).ConfigureAwait(false);
            }
            return result;
        }
        catch
        {
            if (startedTransaction)
            {
                await transaction!.RollbackAsync(cancellationToken).ConfigureAwait(false);
            }
            throw;
        }
    }
    #endregion

    #region Refcursor execution
    private async ValueTask<IReadOnlyList<DataTable>> ExecuteOracleRefCursorsAsync(CommandDefinition command, CancellationToken cancellationToken)
    {
        try
        {
            return await ExecuteWithRetryIfAllowedAsync(async ct =>
            {
                await using var dbCommand = await CreateCommandAsync(command, ct).ConfigureAwait(false);
                await dbCommand.ExecuteNonQueryAsync(ct).ConfigureAwait(false);

                var tables = OracleProvider.ReadRefCursorResults(dbCommand);
                var outputs = command.Parameters is { Count: > 0 }
                    ? ParameterHelper.ExtractOutputParameters(dbCommand, command.Parameters)
                    : null;

                AttachOutputsToFirstTable(tables, outputs);

                return tables;
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            var error = MapError(ex);
            throw new DbCallerException(error, ex);
        }
    }

    private async ValueTask<IReadOnlyList<DataTable>> ExecutePostgresRefCursorsAsync(CommandDefinition command, CancellationToken cancellationToken)
    {
        try
        {
            return await ExecuteWithRetryIfAllowedAsync(async ct =>
            {
                // Ensure we have a transaction for opening + fetching refcursors.
                return await WithTransactionScopeAsync(ct, async effectiveTransaction =>
                {
                    await using var dbCommand = await CreateCommandAsync(command, ct).ConfigureAwait(false);
                    dbCommand.Transaction = effectiveTransaction;

                    await dbCommand.ExecuteNonQueryAsync(ct).ConfigureAwait(false);

                    var tables = await PostgreSqlProvider
                        .ReadRefCursorResultsAsync(dbCommand, _connection!, effectiveTransaction, ct)
                        .ConfigureAwait(false);

                    var outputs = command.Parameters is { Count: > 0 }
                        ? ParameterHelper.ExtractOutputParameters(dbCommand, command.Parameters)
                        : null;

                    AttachOutputsToFirstTable(tables, outputs);

                    return tables;
                }).ConfigureAwait(false);
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            var error = MapError(ex);
            throw new DbCallerException(error, ex);
        }
    }

    private static void AttachOutputsToFirstTable(IReadOnlyList<DataTable> tables, IReadOnlyDictionary<string, object?>? outputs)
    {
        if (outputs is null || tables.Count == 0)
        {
            return;
        }

        // Attach once; outputs are identical for all result sets.
        tables[0].ExtendedProperties["OutputParameters"] = outputs;
    }
    #endregion
}
