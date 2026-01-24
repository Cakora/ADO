using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using AdoAsync.Abstractions;
using AdoAsync.BulkCopy.LinqToDb.Common;
using AdoAsync.Helpers;
using AdoAsync.Validation;
using Polly;

namespace AdoAsync.Execution;

public sealed partial class DbExecutor
{
    #region Public API - Lifetime
    /// <summary>Dispose the shared connection if this executor created it.</summary>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_connection is not null)
        {
            await _connection.DisposeAsync().ConfigureAwait(false);
        }
    }
    #endregion

    #region Private - Connection
    private async ValueTask EnsureConnectionAsync(CancellationToken cancellationToken)
    {
        ThrowIfDisposed();

        if (_connection is null)
        {
            _connection = _options.DataSource is not null
                ? _options.DataSource.CreateConnection()
                : _provider.CreateConnection(_options.ConnectionString ?? throw new DatabaseException(ErrorCategory.Configuration, "ConnectionString is required."));
        }

        if (_connection.State != ConnectionState.Open)
        {
            await _connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private async ValueTask<DbCommand> CreateCommandAsync(CommandDefinition definition, CancellationToken cancellationToken)
    {
        await EnsureConnectionAsync(cancellationToken).ConfigureAwait(false);
        var connection = _connection ?? throw new DatabaseException(ErrorCategory.State, "Connection was not initialized.");
        var dbCommand = _provider.CreateCommand(connection, definition);
        if (_activeTransaction is not null)
        {
            dbCommand.Transaction = _activeTransaction;
        }

        if (definition.Parameters is { } parameters)
        {
            _provider.ApplyParameters(dbCommand, parameters);
        }

        return dbCommand;
    }
    #endregion

    #region Private - Validation
    private DbError? ValidateCommandDefinition(CommandDefinition command) =>
        ValidationRunner.ValidateCommand(command, _options.EnableValidation, _commandValidator, _parameterValidator);
    #endregion

    #region Private - Resilience
    /// <summary>Throws when the executor has been disposed to avoid using torn state.</summary>
    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new DatabaseException(ErrorCategory.Disposed, "DbExecutor has been disposed.");
        }
    }

    private Task<T> ExecuteWithRetryIfAllowedAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken)
    {
        // Never retry inside an explicit user transaction; keep at-most-once semantics.
        if (_activeTransaction is not null || !_options.EnableRetry)
        {
            return action(cancellationToken);
        }

        return _retryPolicy.ExecuteAsync(action, cancellationToken);
    }
    #endregion

    #region Private - Output Parameters
    private static IReadOnlyDictionary<string, object?> ExtractOutputParametersOrEmpty(DbCommand command, IReadOnlyList<DbParameter>? declaredParameters)
    {
        if (!ParameterHelper.HasNonRefCursorOutputs(declaredParameters))
        {
            return EmptyOutputParameters;
        }

        return ParameterHelper.ExtractOutputParameters(command, declaredParameters) ?? EmptyOutputParameters;
    }
    #endregion

    #region Private - Disposal
    private static async ValueTask DisposeCommandAsync(DbCommand? command)
    {
        if (command is not null)
        {
            await command.DisposeAsync().ConfigureAwait(false);
        }
    }
    #endregion

    #region Private - Errors
    private DbCallerException WrapException(Exception exception)
    {
        if (exception is DbCallerException callerException)
        {
            return callerException;
        }

        return _options.WrapProviderExceptions
            ? new DbCallerException(MapError(exception), exception)
            : throw exception;
    }
    #endregion

    #region Private - Bulk Options
    private LinqToDbBulkOptions ResolveLinqToDbOptions(LinqToDbBulkOptions? overrides)
    {
        var defaults = _options.LinqToDb ?? new LinqToDbBulkOptions();
        if (overrides is null)
        {
            return defaults;
        }

        return defaults with
        {
            Enable = overrides.Enable || defaults.Enable,
            BulkCopyType = overrides.BulkCopyType,
            BulkCopyTimeoutSeconds = overrides.BulkCopyTimeoutSeconds ?? defaults.BulkCopyTimeoutSeconds,
            MaxBatchSize = overrides.MaxBatchSize ?? defaults.MaxBatchSize,
            NotifyAfter = overrides.NotifyAfter ?? defaults.NotifyAfter,
            KeepIdentity = overrides.KeepIdentity ?? defaults.KeepIdentity,
            CheckConstraints = overrides.CheckConstraints ?? defaults.CheckConstraints,
            KeepNulls = overrides.KeepNulls ?? defaults.KeepNulls,
            FireTriggers = overrides.FireTriggers ?? defaults.FireTriggers,
            TableLock = overrides.TableLock ?? defaults.TableLock,
            UseInternalTransaction = overrides.UseInternalTransaction ?? defaults.UseInternalTransaction,
            UseParameters = overrides.UseParameters ?? defaults.UseParameters,
            MaxParametersForBatch = overrides.MaxParametersForBatch ?? defaults.MaxParametersForBatch,
            MaxDegreeOfParallelism = overrides.MaxDegreeOfParallelism ?? defaults.MaxDegreeOfParallelism,
            OnRowsCopied = overrides.OnRowsCopied ?? defaults.OnRowsCopied
        };
    }
    #endregion

    #region Private - Error Mapping
    private DbError MapError(Exception exception)
    {
        if (exception is DbCallerException callerException)
        {
            return callerException.Error;
        }

        return Exceptions.ExceptionHandler.Map(_options.DatabaseType, exception);
    }
    #endregion

    #region Private - Provider Resolution
    private static IDbProvider ResolveProvider(DatabaseType databaseType) =>
        ProviderHelper.ResolveProvider(databaseType);

    private static DbError MapProviderError(DatabaseType databaseType, Exception exception) =>
        ProviderHelper.MapProviderError(databaseType, exception);
    #endregion
}
