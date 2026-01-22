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
    /// <summary>
    /// For refcursor scenarios: reuse the caller's transaction when present; otherwise start a short-lived transaction.
    /// </summary>
    private async ValueTask<DbTransaction?> GetRefCursorTransactionAsync(CancellationToken cancellationToken)
    {
        await EnsureConnectionAsync(cancellationToken).ConfigureAwait(false);
        return _activeTransaction is null
            ? await _connection!.BeginTransactionAsync(cancellationToken).ConfigureAwait(false)
            : null;
    }
    #endregion

    #region Refcursor execution
    private async IAsyncEnumerable<(int SetIndex, T Item)> StreamOracleRefCursors<T>(
        CommandDefinition command,
        Func<IDataRecord, T>[] mappers,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await using var dbCommand = await CreateCommandAsync(command, cancellationToken).ConfigureAwait(false);
        await dbCommand.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

        var cursors = OracleProvider.ReadRefCursorResults(dbCommand);
        if (cursors.Count > mappers.Length)
        {
            throw new DatabaseException(ErrorCategory.Unsupported, "Additional result sets exist without corresponding mappers.");
        }

        var setIndex = 0;
        foreach (var table in cursors)
        {
            var map = mappers[setIndex] ?? throw new ArgumentNullException($"mappers[{setIndex}]");
            using var reader = table.CreateDataReader();
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return (setIndex, map(reader));
            }
            setIndex++;
        }
    }

    private async ValueTask<DbResult> ExecuteOracleRefCursorsAsync(CommandDefinition command, CancellationToken cancellationToken)
    {
        try
        {
            return await ExecuteWithRetryIfAllowedAsync(async ct =>
            {
                await using var dbCommand = await CreateCommandAsync(command, ct).ConfigureAwait(false);
                await dbCommand.ExecuteNonQueryAsync(ct).ConfigureAwait(false);

                var tables = OracleProvider.ReadRefCursorResults(dbCommand);
                return new DbResult
                {
                    Success = true,
                    Tables = tables,
                    OutputParameters = command.Parameters is { Count: > 0 }
                        ? ParameterHelper.ExtractOutputParameters(dbCommand, command.Parameters)
                        : null
                };
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            var error = MapError(ex);
            return new DbResult { Success = false, Error = error };
        }
    }

    private async ValueTask<DbResult> ExecutePostgresRefCursorsAsync(CommandDefinition command, CancellationToken cancellationToken)
    {
        try
        {
            return await ExecuteWithRetryIfAllowedAsync(async ct =>
            {
                // Refcursor fetches require a transaction scope in PostgreSQL; start one only when the caller has not.
                await using var transaction = await GetRefCursorTransactionAsync(ct).ConfigureAwait(false);
                await using var dbCommand = await CreateCommandAsync(command, ct).ConfigureAwait(false);
                if (transaction is not null)
                {
                    dbCommand.Transaction = transaction;
                }
                try
                {
                    await dbCommand.ExecuteNonQueryAsync(ct).ConfigureAwait(false);

                    var tables = await PostgreSqlProvider
                        .ReadRefCursorResultsAsync(dbCommand, _connection!, transaction ?? _activeTransaction!, ct)
                        .ConfigureAwait(false);

                    if (transaction is not null)
                    {
                        await transaction.CommitAsync(ct).ConfigureAwait(false);
                    }

                    return new DbResult
                    {
                        Success = true,
                        Tables = tables,
                        OutputParameters = command.Parameters is { Count: > 0 }
                            ? ParameterHelper.ExtractOutputParameters(dbCommand, command.Parameters)
                            : null
                    };
                }
                catch
                {
                    if (transaction is not null)
                    {
                        await transaction.RollbackAsync(ct).ConfigureAwait(false);
                    }
                    throw;
                }
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            var error = MapError(ex);
            return new DbResult { Success = false, Error = error };
        }
    }
    #endregion
}
