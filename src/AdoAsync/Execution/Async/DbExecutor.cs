using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Runtime.CompilerServices;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AdoAsync.Abstractions;
using AdoAsync.BulkCopy.LinqToDb.Common;
using AdoAsync.BulkCopy.LinqToDb.Typed;
using AdoAsync.Extensions.Execution;
using AdoAsync.Helpers;
using AdoAsync.Resilience;
using AdoAsync.Transactions;
using AdoAsync.Validation;
using FluentValidation;
using Polly;

namespace AdoAsync.Execution;

/// <summary>
/// Orchestrates validation → resilience → provider → ADO.NET. Not thread-safe. Streaming by default; materialization is explicit and allocation-heavy. Retries are Polly-based and opt-in; cancellation is honored on all async calls; transactions are explicit via the transaction manager.
/// </summary>
public sealed partial class DbExecutor : IDbExecutor
{
    #region Fields
    private const string OutputsNotSupportedMessage = "Output parameters are not returned on this path. Use a buffered method instead.";
    private readonly DbOptions _options;
    private readonly IDbProvider _provider;
    private readonly IAsyncPolicy _retryPolicy;
    private readonly IValidator<CommandDefinition> _commandValidator;
    private readonly IValidator<DbParameter> _parameterValidator;
    private readonly IValidator<BulkImportRequest> _bulkImportValidator;
    private readonly ILinqToDbTypedBulkImporter _linqToDbBulkImporter;
    private DbTransaction? _activeTransaction;
    private DbConnection? _connection;
    private bool _disposed;
    #endregion

    #region Constructors

    private DbExecutor(
        DbOptions options,
        IDbProvider provider,
        IAsyncPolicy retryPolicy,
        IValidator<CommandDefinition> commandValidator,
        IValidator<DbParameter> parameterValidator,
        IValidator<BulkImportRequest> bulkImportValidator,
        ILinqToDbTypedBulkImporter linqToDbBulkImporter)
    {
        _options = options;
        _provider = provider;
        _retryPolicy = retryPolicy;
        _commandValidator = commandValidator;
        _parameterValidator = parameterValidator;
        _bulkImportValidator = bulkImportValidator;
        _linqToDbBulkImporter = linqToDbBulkImporter;
    }
    #endregion

    #region Factory
    /// <summary>Creates a new executor for the specified options.</summary>
    public static DbExecutor Create(DbOptions options, bool isInUserTransaction = false)
    {
        Validate.Required(options, nameof(options));

        var provider = ResolveProvider(options.DatabaseType);
        var optionsValidator = new DbOptionsValidator();
        var commandValidator = new CommandDefinitionValidator();
        var parameterValidator = new DbParameterValidator();
        var bulkImportValidator = new BulkImportRequestValidator();
        var linqToDbConnectionFactory = new LinqToDbConnectionFactory(options.DatabaseType);
        var linqToDbBulkImporter = new LinqToDbTypedBulkImporter(linqToDbConnectionFactory);

        var retryPolicy = RetryPolicyFactory.Create(
            options,
            exception => MapProviderError(options.DatabaseType, exception).IsTransient,
            isInUserTransaction);

        var validationError = ValidationRunner.ValidateOptions(options, options.EnableValidation, optionsValidator);
        if (validationError is not null)
        {
            throw new DatabaseException(ErrorCategory.Configuration, $"Invalid DbOptions: {validationError.MessageKey}");
        }

        return new DbExecutor(options, provider, retryPolicy, commandValidator, parameterValidator, bulkImportValidator, linqToDbBulkImporter);
    }
    #endregion

    #region Public API
    /// <summary>Execute a single SELECT and return a streaming reader.</summary>
    public async ValueTask<DbDataReader> ExecuteReaderAsync(CommandDefinition command, CancellationToken cancellationToken = default)
    {
        await EnsureNotDisposedAsync().ConfigureAwait(false);
        var validationError = ValidateCommandDefinition(command);
        if (validationError is not null)
        {
            throw new DbCallerException(validationError);
        }
        ThrowIfOutputsNotSupported(command);

        try
        {
            var dbCommand = await CreateCommandAsync(command, cancellationToken).ConfigureAwait(false);
            return await dbCommand.ExecuteReaderAsync(command.Behavior, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            throw WrapException(ex);
        }
    }

    /// <summary>Execute a single SELECT and return a streaming reader with deferred output parameters (SQL Server/PostgreSQL only).</summary>
    public async ValueTask<StreamingReaderResult> ExecuteReaderWithOutputsAsync(CommandDefinition command, CancellationToken cancellationToken = default)
    {
        await EnsureNotDisposedAsync().ConfigureAwait(false);
        var validationError = ValidateCommandDefinition(command);
        if (validationError is not null)
        {
            throw new DbCallerException(validationError);
        }

        if (_options.DatabaseType == DatabaseType.Oracle)
        {
            throw new DbCallerException(DbErrorMapper.Map(new DatabaseException(ErrorCategory.Unsupported, "Oracle does not support streaming outputs. Use buffered materialization instead.")));
        }

        if (CursorHelper.IsPostgresRefCursor(_options.DatabaseType, command))
        {
            throw new DbCallerException(DbErrorMapper.Map(new DatabaseException(ErrorCategory.Unsupported, "PostgreSQL refcursor requires buffered materialization (QueryTableAsync/ExecuteDataSetAsync).")));
        }

        try
        {
            var dbCommand = await CreateCommandAsync(command, cancellationToken).ConfigureAwait(false);
            var reader = await dbCommand.ExecuteReaderAsync(command.Behavior, cancellationToken).ConfigureAwait(false);
            return new StreamingReaderResult(dbCommand, reader, command.Parameters);
        }
        catch (Exception ex)
        {
            throw WrapException(ex);
        }
    }

    /// <summary>Stream rows for a single SELECT.</summary>
    public async IAsyncEnumerable<IDataRecord> StreamAsync(CommandDefinition command, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (_options.DatabaseType == DatabaseType.Oracle)
        {
            // Oracle cursors require buffering; disallow streaming to avoid partial results.
            throw new DatabaseException(ErrorCategory.Unsupported, "Streaming is not supported for Oracle in StreamAsync. Use buffered materialization instead.");
        }

        await using var reader = await ExecuteReaderAsync(command, cancellationToken).ConfigureAwait(false);
        await foreach (var record in reader.StreamRecordsAsync(cancellationToken).ConfigureAwait(false))
        {
            yield return record;
        }
    }

    /// <summary>Execute a command that returns only a row count.</summary>
    public async ValueTask<int> ExecuteAsync(CommandDefinition command, CancellationToken cancellationToken = default)
    {
        await EnsureNotDisposedAsync().ConfigureAwait(false);
        var validationError = ValidateCommandDefinition(command);
        if (validationError is not null)
        {
            throw new DbCallerException(validationError);
        }

        try
        {
            return await ExecuteWithRetryIfAllowedAsync(async ct =>
            {
                await using var dbCommand = await CreateCommandAsync(command, ct).ConfigureAwait(false);
                return await dbCommand.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            throw WrapException(ex);
        }
    }

    /// <summary>Execute a command and convert the scalar result to <typeparamref name="T"/>.</summary>
    public async ValueTask<T> ExecuteScalarAsync<T>(CommandDefinition command, CancellationToken cancellationToken = default)
    {
        await EnsureNotDisposedAsync().ConfigureAwait(false);
        var validationError = ValidateCommandDefinition(command);
        if (validationError is not null)
        {
            throw new DbCallerException(validationError);
        }
        ThrowIfOutputsNotSupported(command);

        try
        {
            return await ExecuteWithRetryIfAllowedAsync(async ct =>
            {
                await using var dbCommand = await CreateCommandAsync(command, ct).ConfigureAwait(false);
                var value = await dbCommand.ExecuteScalarAsync(ct).ConfigureAwait(false);
                if (value is null or DBNull)
                {
                    return default!;
                }

                if (value is T typed)
                {
                    return typed;
                }

                // Convert.ChangeType keeps scalar conversions consistent across providers.
                return (T)Convert.ChangeType(value, typeof(T));
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            throw WrapException(ex);
        }
    }

    /// <summary>Buffer rows with an explicit mapper.</summary>
    public async ValueTask<List<T>> QueryAsync<T>(CommandDefinition command, Func<DataRow, T> map, CancellationToken cancellationToken = default)
    {
        Validate.Required(map, nameof(map));
        var validationError = ValidateCommandDefinition(command);
        if (validationError is not null)
        {
            throw new DbCallerException(validationError);
        }

        try
        {
            var table = await QueryTableAsync(command, cancellationToken).ConfigureAwait(false);
            return table.ToList(map);
        }
        catch (Exception ex)
        {
            throw WrapException(ex);
        }
    }

    /// <summary>Stream multiple result sets with per-set mappers (non-buffered; not for refcursor procs).</summary>
    public IAsyncEnumerable<(int SetIndex, T Item)> QueryMultiAsync<T>(
        CommandDefinition command,
        Func<IDataRecord, T>[] mappers,
        CancellationToken cancellationToken = default)
    {
        // Each mapper corresponds to one result set.
        Validate.Required(mappers, nameof(mappers));
        if (mappers.Length == 0)
        {
            throw new ArgumentException("At least one mapper is required.", nameof(mappers));
        }

        return QueryMultiAsyncIterator(command, mappers, cancellationToken);
    }

    private async IAsyncEnumerable<(int SetIndex, T Item)> QueryMultiAsyncIterator<T>(
        CommandDefinition command,
        Func<IDataRecord, T>[] mappers,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        // Guard executor state and command validity.
        await EnsureNotDisposedAsync().ConfigureAwait(false);
        var validationError = ValidateCommandDefinition(command);
        if (validationError is not null)
        {
            throw new DbCallerException(validationError);
        }

        if (ShouldUseOracleRefCursorPath(command))
        {
            await foreach (var row in StreamOracleRefCursors(command, mappers, cancellationToken))
            {
                yield return row;
            }
            yield break;
        }

        if (ShouldUsePostgresRefCursorPath(command))
        {
            await foreach (var row in StreamPostgresRefCursors(command, mappers, cancellationToken))
            {
                yield return row;
            }
            yield break;
        }

        // Execute command and stream result sets.
        await using var dbCommand = await CreateCommandAsync(command, cancellationToken).ConfigureAwait(false);
        await using var reader = await dbCommand.ExecuteReaderAsync(CommandBehavior.Default, cancellationToken).ConfigureAwait(false);

        for (var setIndex = 0; setIndex < mappers.Length; setIndex++)
        {
            // Mapper for current result set.
            var map = mappers[setIndex] ?? throw new ArgumentNullException($"mappers[{setIndex}]");

            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return (setIndex, map(reader));
            }

            // Move to next result set; stop if none.
            if (!await reader.NextResultAsync(cancellationToken).ConfigureAwait(false))
            {
                yield break;
            }
        }

        // Additional result sets without mappers are not supported.
        if (await reader.NextResultAsync(cancellationToken).ConfigureAwait(false))
        {
            throw new DatabaseException(ErrorCategory.Unsupported, "Additional result sets exist without corresponding mappers.");
        }
    }

    private async IAsyncEnumerable<(int SetIndex, T Item)> StreamPostgresRefCursors<T>(
        CommandDefinition command,
        Func<IDataRecord, T>[] mappers,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await using var transaction = await GetRefCursorTransactionAsync(cancellationToken).ConfigureAwait(false);

        await using var dbCommand = await CreateCommandAsync(command, cancellationToken).ConfigureAwait(false);
        if (transaction is not null)
        {
            dbCommand.Transaction = transaction;
        }

        await dbCommand.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

        var cursorNames = CursorHelper.CollectPostgresCursorNames(dbCommand);

        if (cursorNames.Count > mappers.Length)
        {
            throw new DatabaseException(ErrorCategory.Unsupported, "Additional result sets exist without corresponding mappers.");
        }

        var setIndex = 0;
        foreach (var cursor in cursorNames)
        {
            var fetchCommand = _connection!.CreateCommand();
            fetchCommand.Transaction = transaction ?? _activeTransaction;
            fetchCommand.CommandText = $"fetch all from {cursor};";
            fetchCommand.CommandType = CommandType.Text;
            await using var reader = await fetchCommand.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

            var map = mappers[setIndex] ?? throw new ArgumentNullException($"mappers[{setIndex}]");
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return (setIndex, map(reader));
            }

            setIndex++;
        }

        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Materialize result sets into tables for callers that need DataTable/DataSet.</summary>
    public async ValueTask<DbResult> QueryTablesAsync(CommandDefinition command, CancellationToken cancellationToken = default)
    {
        await EnsureNotDisposedAsync().ConfigureAwait(false);
        var validationError = ValidateCommandDefinition(command);
        if (validationError is not null)
        {
            return new DbResult { Success = false, Error = validationError };
        }

        if (ShouldUseOracleRefCursorPath(command))
        {
            return await ExecuteOracleRefCursorsAsync(command, cancellationToken).ConfigureAwait(false);
        }

        if (ShouldUsePostgresRefCursorPath(command))
        {
            return await ExecutePostgresRefCursorsAsync(command, cancellationToken).ConfigureAwait(false);
        }

        try
        {
            return await ExecuteWithRetryIfAllowedAsync(async ct =>
            {
                await using var dbCommand = await CreateCommandAsync(command, ct).ConfigureAwait(false);
                var tables = await DataAdapterHelper.FillTablesAsync(dbCommand, ct).ConfigureAwait(false);

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

    /// <summary>Buffered single result as DataTable.</summary>
    public async ValueTask<DataTable> QueryTableAsync(CommandDefinition command, CancellationToken cancellationToken = default)
    {
        await EnsureNotDisposedAsync().ConfigureAwait(false);
        var validationError = ValidateCommandDefinition(command);
        if (validationError is not null)
        {
            throw new DbCallerException(validationError);
        }

        // Oracle and PostgreSQL refcursor paths rely on DataAdapter to hydrate cursor outputs.
        if (CursorHelper.IsOracleRefCursor(_options.DatabaseType, command) ||
            CursorHelper.IsPostgresRefCursor(_options.DatabaseType, command))
        {
            // Reuse the multi-result buffered path so cursor handling stays consistent.
            var multi = await QueryTablesAsync(command, cancellationToken).ConfigureAwait(false);
            if (!multi.Success)
            {
                throw new DbCallerException(multi.Error ?? DbErrorMapper.Map(new DatabaseException(ErrorCategory.State, "QueryTableAsync failed.")));
            }

            var table = multi.Tables is { Count: > 0 } ? multi.Tables[0] : new DataTable();
            if (multi.OutputParameters is not null)
            {
                table.ExtendedProperties["OutputParameters"] = multi.OutputParameters;
            }
            return table;
        }

        try
        {
            return await ExecuteWithRetryIfAllowedAsync(async ct =>
            {
                await using var dbCommand = await CreateCommandAsync(command, ct).ConfigureAwait(false);
                var table = await DataAdapterHelper.FillTableAsync(dbCommand, ct).ConfigureAwait(false);
                var outputs = command.Parameters is { Count: > 0 }
                    ? ParameterHelper.ExtractOutputParameters(dbCommand, command.Parameters)
                    : null;
                if (outputs is not null)
                {
                    table.ExtendedProperties["OutputParameters"] = outputs;
                }
                return table;
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            throw WrapException(ex);
        }
    }

    /// <summary>Buffered multi-result as DataSet.</summary>
    public async ValueTask<DataSet> ExecuteDataSetAsync(CommandDefinition command, CancellationToken cancellationToken = default)
    {
        await EnsureNotDisposedAsync().ConfigureAwait(false);
        var validationError = ValidateCommandDefinition(command);
        if (validationError is not null)
        {
            throw new DbCallerException(validationError);
        }

        var (dataSet, outputs) = await ExecuteDataSetInternalAsync(command, cancellationToken).ConfigureAwait(false);
        if (outputs is not null)
        {
            // Surface output parameters without altering the return type.
            dataSet.ExtendedProperties["OutputParameters"] = outputs;
        }

        return dataSet;
    }

    /// <summary>Bulk import data using provider-specific fast paths.</summary>
    public async ValueTask<BulkImportResult> BulkImportAsync(BulkImportRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureNotDisposedAsync().ConfigureAwait(false);

        var validationError = ValidationRunner.ValidateBulkImport(request, _options.EnableValidation, _bulkImportValidator);
        if (validationError is not null)
        {
            return new BulkImportResult { Success = false, Error = validationError };
        }

        try
        {
            return await ExecuteWithRetryIfAllowedAsync(async ct =>
            {
                await EnsureConnectionAsync(ct).ConfigureAwait(false);
                var started = Stopwatch.StartNew();
                var normalizedRequest = request;
                if (_options.DatabaseType == DatabaseType.Oracle)
                {
                    normalizedRequest = request with
                    {
                        DestinationTable = IdentifierNormalization.NormalizeTableName(_options.DatabaseType, request.DestinationTable)
                    };
                }

                var rows = await _provider.BulkImportAsync(_connection!, _activeTransaction, normalizedRequest, ct).ConfigureAwait(false);
                started.Stop();
                return new BulkImportResult
                {
                    Success = true,
                    RowsInserted = rows,
                    Duration = started.Elapsed
                };
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            var error = MapError(ex);
            return new BulkImportResult { Success = false, Error = error };
        }
    }

    /// <summary>Bulk import typed rows using linq2db. Requires linq2db bulk copy to be enabled.</summary>
    public async ValueTask<BulkImportResult> BulkImportAsync<T>(
        IEnumerable<T> items,
        string? tableName = null,
        LinqToDbBulkOptions? bulkOptions = null,
        CancellationToken cancellationToken = default) where T : class
    {
        await EnsureNotDisposedAsync().ConfigureAwait(false);
        Validate.Required(items, nameof(items));

        var resolvedOptions = ResolveLinqToDbOptions(bulkOptions);
        if (!resolvedOptions.Enable)
        {
            var error = DbErrorMapper.Map(new DatabaseException(ErrorCategory.Configuration, "LinqToDB bulk copy is disabled. Enable DbOptions.LinqToDb.Enable to use typed bulk imports."));
            return new BulkImportResult { Success = false, Error = error };
        }

        try
        {
            return await ExecuteWithRetryIfAllowedAsync(async ct =>
            {
                await EnsureConnectionAsync(ct).ConfigureAwait(false);
                var started = Stopwatch.StartNew();
                var rows = await _linqToDbBulkImporter.BulkImportAsync(_connection!, _activeTransaction, items, resolvedOptions, _options.CommandTimeoutSeconds, tableName, ct).ConfigureAwait(false);
                started.Stop();
                return new BulkImportResult
                {
                    Success = true,
                    RowsInserted = rows,
                    Duration = started.Elapsed
                };
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            var error = MapError(ex);
            return new BulkImportResult { Success = false, Error = error };
        }
    }

    /// <summary>Bulk import async-typed rows using linq2db. Requires linq2db bulk copy to be enabled.</summary>
    public async ValueTask<BulkImportResult> BulkImportAsync<T>(
        IAsyncEnumerable<T> items,
        string? tableName = null,
        LinqToDbBulkOptions? bulkOptions = null,
        CancellationToken cancellationToken = default) where T : class
    {
        await EnsureNotDisposedAsync().ConfigureAwait(false);
        Validate.Required(items, nameof(items));

        var resolvedOptions = ResolveLinqToDbOptions(bulkOptions);
        if (!resolvedOptions.Enable)
        {
            var error = DbErrorMapper.Map(new DatabaseException(ErrorCategory.Configuration, "LinqToDB bulk copy is disabled. Enable DbOptions.LinqToDb.Enable to use typed bulk imports."));
            return new BulkImportResult { Success = false, Error = error };
        }

        try
        {
            return await ExecuteWithRetryIfAllowedAsync(async ct =>
            {
                await EnsureConnectionAsync(ct).ConfigureAwait(false);
                var started = Stopwatch.StartNew();
                var rows = await _linqToDbBulkImporter.BulkImportAsync(_connection!, _activeTransaction, items, resolvedOptions, _options.CommandTimeoutSeconds, tableName, ct).ConfigureAwait(false);
                started.Stop();
                return new BulkImportResult
                {
                    Success = true,
                    RowsInserted = rows,
                    Duration = started.Elapsed
                };
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            var error = MapError(ex);
            return new BulkImportResult { Success = false, Error = error };
        }
    }

    /// <summary>Begins an explicit transaction on the shared connection (rollback-on-dispose unless committed).</summary>
    public async ValueTask<TransactionHandle> BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        await EnsureNotDisposedAsync().ConfigureAwait(false);
        await EnsureConnectionAsync(cancellationToken).ConfigureAwait(false);

        if (_activeTransaction is not null)
        {
            throw new DatabaseException(ErrorCategory.State, "A transaction is already active.");
        }

        var transactionManager = new TransactionManager(_connection!);
        var handle = await transactionManager
            .BeginAsync(_connection!, onDispose: () => _activeTransaction = null, cancellationToken)
            .ConfigureAwait(false);

        _activeTransaction = handle.Transaction;
        return handle;
    }

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

    #region Private Helpers

    #region Connection helpers
    private async ValueTask EnsureConnectionAsync(CancellationToken cancellationToken)
    {
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
        var dbCommand = _provider.CreateCommand(_connection!, definition);
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

    #region Command routing and validation
    private DbError? ValidateCommandDefinition(CommandDefinition command) =>
        ValidationRunner.ValidateCommand(command, _options.EnableValidation, _commandValidator, _parameterValidator);
    #endregion

    #region Execution and resilience helpers

    /// <summary>Throws when the executor has been disposed to avoid using torn state.</summary>
    private async ValueTask EnsureNotDisposedAsync()
    {
        if (_disposed)
        {
            throw new DatabaseException(ErrorCategory.Disposed, "DbExecutor has been disposed.");
        }
        await Task.CompletedTask;
    }

    private static void ThrowIfOutputsNotSupported(CommandDefinition command)
    {
        if (command.Parameters is null || command.Parameters.Count == 0)
        {
            return;
        }

        foreach (var parameter in command.Parameters)
        {
            if (parameter.Direction != ParameterDirection.Input)
            {
                throw new DbCallerException(DbErrorMapper.Map(new DatabaseException(ErrorCategory.Unsupported, OutputsNotSupportedMessage)));
            }
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

    private DbError MapError(Exception exception)
    {
        if (exception is DbCallerException callerException)
        {
            return callerException.Error;
        }

        return Exceptions.ExceptionHandler.Map(_options.DatabaseType, exception);
    }

    private async ValueTask<(DataSet DataSet, IReadOnlyDictionary<string, object?>? OutputParameters)> ExecuteDataSetInternalAsync(
        CommandDefinition command,
        CancellationToken cancellationToken)
    {
        try
        {
            return await ExecuteWithRetryIfAllowedAsync(async ct =>
            {
                await using var dbCommand = await CreateCommandAsync(command, ct).ConfigureAwait(false);

                // Cursor scenarios rely on DataAdapter fill; this path is used for all providers.
                var dataSet = await DataAdapterHelper.FillDataSetAsync(dbCommand, ct).ConfigureAwait(false);
                var outputs = command.Parameters is { Count: > 0 }
                    ? ParameterHelper.ExtractOutputParameters(dbCommand, command.Parameters)
                    : null;

                return (dataSet, outputs);
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            throw WrapException(ex);
        }
    }

    #endregion

    #region Provider resolution
    private static IDbProvider ResolveProvider(DatabaseType databaseType) =>
        ProviderHelper.ResolveProvider(databaseType);

    private static DbError MapProviderError(DatabaseType databaseType, Exception exception) =>
        ProviderHelper.MapProviderError(databaseType, exception);
    #endregion
    #endregion
}
