using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Runtime.CompilerServices;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using AdoAsync;
using AdoAsync.Abstractions;
using AdoAsync.Providers.Oracle;
using AdoAsync.Providers.PostgreSql;
using AdoAsync.Providers.SqlServer;
using AdoAsync.Resilience;
using AdoAsync.Validation;
using FluentValidation;
using Polly;

namespace AdoAsync.Execution;

/// <summary>
/// Orchestrates validation → resilience → provider → ADO.NET. Not thread-safe. Streaming by default; materialization is explicit and allocation-heavy. Retries are Polly-based and opt-in; cancellation is honored on all async calls; transactions are explicit via the transaction manager.
/// </summary>
public sealed class DbExecutor : IDbExecutor
{
    #region Fields
    private readonly DbOptions _options;
    private readonly IDbProvider _provider;
    private readonly IAsyncPolicy _retryPolicy;
    private readonly IValidator<CommandDefinition> _commandValidator;
    private readonly IValidator<DbParameter> _parameterValidator;
    private readonly IValidator<BulkImportRequest> _bulkImportValidator;
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
        IValidator<BulkImportRequest> bulkImportValidator)
    {
        _options = options;
        _provider = provider;
        _retryPolicy = retryPolicy;
        _commandValidator = commandValidator;
        _parameterValidator = parameterValidator;
        _bulkImportValidator = bulkImportValidator;
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

        var retryPolicy = RetryPolicyFactory.Create(
            options,
            exception => MapProviderError(options.DatabaseType, exception).IsTransient,
            isInUserTransaction);

        var validationError = ValidationOrchestrator.ValidateOptions(options, options.EnableValidation, optionsValidator);
        if (validationError is not null)
        {
            throw new DatabaseException(ErrorCategory.Configuration, $"Invalid DbOptions: {validationError.MessageKey}");
        }

        return new DbExecutor(options, provider, retryPolicy, commandValidator, parameterValidator, bulkImportValidator);
    }
    #endregion

    #region Public API
    public async ValueTask<int> ExecuteAsync(CommandDefinition command, CancellationToken cancellationToken = default)
    {
        await EnsureNotDisposedAsync().ConfigureAwait(false);
        var validationError = ValidationOrchestrator.ValidateCommand(command, _options.EnableValidation, _commandValidator, _parameterValidator);
        if (validationError is not null)
        {
            throw new DatabaseException(ErrorCategory.Validation, validationError.MessageKey);
        }

        // Wrap execution in the retry policy to keep retry behavior centralized.
        return await _retryPolicy.ExecuteAsync(async ct =>
        {
            await using var dbCommand = await CreateCommandAsync(command, ct).ConfigureAwait(false);
            return await dbCommand.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<T> ExecuteScalarAsync<T>(CommandDefinition command, CancellationToken cancellationToken = default)
    {
        await EnsureNotDisposedAsync().ConfigureAwait(false);
        var validationError = ValidationOrchestrator.ValidateCommand(command, _options.EnableValidation, _commandValidator, _parameterValidator);
        if (validationError is not null)
        {
            throw new DatabaseException(ErrorCategory.Validation, validationError.MessageKey);
        }

        return await _retryPolicy.ExecuteAsync(async ct =>
        {
            await using var dbCommand = await CreateCommandAsync(command, ct).ConfigureAwait(false);
            // Convert.ChangeType keeps ExecuteScalar generic without duplicating per-type logic.
            var value = await dbCommand.ExecuteScalarAsync(ct).ConfigureAwait(false);
            if (value is null or DBNull)
            {
                return default!;
            }

            if (value is T typed)
            {
                return typed;
            }

            return (T)Convert.ChangeType(value, typeof(T));
        }, cancellationToken).ConfigureAwait(false);
    }

    public IAsyncEnumerable<T> QueryAsync<T>(CommandDefinition command, Func<IDataRecord, T> map, CancellationToken cancellationToken = default)
    {
        Validate.Required(map, nameof(map));
        var validationError = ValidationOrchestrator.ValidateCommand(command, _options.EnableValidation, _commandValidator, _parameterValidator);
        if (validationError is not null)
        {
            throw new DatabaseException(ErrorCategory.Validation, validationError.MessageKey);
        }

        // Iterator defers execution until enumeration, keeping streaming behavior explicit.
        return QueryAsyncIterator(command, map, cancellationToken);
    }

    public async ValueTask<DbResult> QueryTablesAsync(CommandDefinition command, CancellationToken cancellationToken = default)
    {
        await EnsureNotDisposedAsync().ConfigureAwait(false);
        var validationError = ValidationOrchestrator.ValidateCommand(command, _options.EnableValidation, _commandValidator, _parameterValidator);
        if (validationError is not null)
        {
            return new DbResult { Success = false, Error = validationError };
        }

        try
        {
            return await _retryPolicy.ExecuteAsync(async ct =>
            {
                await using var dbCommand = await CreateCommandAsync(command, ct).ConfigureAwait(false);
                var tables = new List<DataTable>();

                await using var reader = await dbCommand.ExecuteReaderAsync(CommandBehavior.Default, ct).ConfigureAwait(false);
                do
                {
                    // DataTable materialization is allocation-heavy by design; kept explicit.
                    var table = new DataTable();
                    table.Load(reader);
                    tables.Add(table);
                } while (await reader.NextResultAsync(ct).ConfigureAwait(false));

                return new DbResult
                {
                    Success = true,
                    Tables = tables
                };
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            var error = MapProviderError(_options.DatabaseType, ex);
            return new DbResult { Success = false, Error = error };
        }
    }

    public async ValueTask<BulkImportResult> BulkImportAsync(BulkImportRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureNotDisposedAsync().ConfigureAwait(false);

        var validationError = ValidationOrchestrator.ValidateBulkImport(request, _options.EnableValidation, _bulkImportValidator);
        if (validationError is not null)
        {
            return new BulkImportResult { Success = false, Error = validationError };
        }

        try
        {
            return await _retryPolicy.ExecuteAsync(async ct =>
            {
                await EnsureConnectionAsync(ct).ConfigureAwait(false);
                var started = Stopwatch.StartNew();
                // Stopwatch captures only the provider call to keep diagnostics focused.
                var rows = await _provider.BulkImportAsync(_connection!, request, ct).ConfigureAwait(false);
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
            var error = MapProviderError(_options.DatabaseType, ex);
            return new BulkImportResult { Success = false, Error = error };
        }
    }

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
    private async ValueTask EnsureConnectionAsync(CancellationToken cancellationToken)
    {
        if (_connection is null)
        {
            _connection = _options.DataSource is not null
                ? _options.DataSource.CreateConnection()
                : _provider.CreateConnection(_options.ConnectionString);
        }

        if (_connection.State != ConnectionState.Open)
        {
            // Open lazily to keep constructor side-effect free and avoid unused connections.
            await _connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private async ValueTask<DbCommand> CreateCommandAsync(CommandDefinition definition, CancellationToken cancellationToken)
    {
        await EnsureConnectionAsync(cancellationToken).ConfigureAwait(false);
        var dbCommand = _provider.CreateCommand(_connection!, definition);
        if (definition.Parameters is { } parameters)
        {
            // Parameters are applied once per command to keep provider logic isolated.
            _provider.ApplyParameters(dbCommand, parameters);
        }

        return dbCommand;
    }

    private async IAsyncEnumerable<T> QueryAsyncIterator<T>(
        CommandDefinition command,
        Func<IDataRecord, T> map,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await EnsureNotDisposedAsync().ConfigureAwait(false);

        await foreach (var item in ExecuteQueryAsync(command, map, cancellationToken).ConfigureAwait(false))
        {
            yield return item;
        }
    }

    private async IAsyncEnumerable<T> ExecuteQueryAsync<T>(
        CommandDefinition command,
        Func<IDataRecord, T> map,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await using var dbCommand = await CreateCommandAsync(command, cancellationToken).ConfigureAwait(false);
        await using var reader = await dbCommand.ExecuteReaderAsync(CommandBehavior.Default, cancellationToken).ConfigureAwait(false);

        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            // Map each row on the fly to keep memory usage proportional to row size.
            yield return map(reader);
        }
    }

    private async ValueTask EnsureNotDisposedAsync()
    {
        if (_disposed)
        {
            throw new DatabaseException(ErrorCategory.Disposed, "DbExecutor has been disposed.");
        }
        await Task.CompletedTask; // Preserve async signature for uniform call sites.
    }

    private static IDbProvider ResolveProvider(DatabaseType databaseType) =>
        databaseType switch
        {
            DatabaseType.SqlServer => new SqlServerProvider(),
            DatabaseType.PostgreSql => new PostgreSqlProvider(),
            DatabaseType.Oracle => new OracleProvider(),
            _ => throw new DatabaseException(ErrorCategory.Unsupported, $"Database type '{databaseType}' is not supported.")
        };

    private static DbError MapProviderError(DatabaseType databaseType, Exception exception) =>
        databaseType switch
        {
            DatabaseType.SqlServer => SqlServerExceptionMapper.Map(exception),
            DatabaseType.PostgreSql => PostgreSqlExceptionMapper.Map(exception),
            DatabaseType.Oracle => OracleExceptionMapper.Map(exception),
            _ => DbErrorMapper.Unknown(exception)
        };
    #endregion
}
