using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Data.Common;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using AdoAsync.Abstractions;
using AdoAsync.BulkCopy.LinqToDb.Common;
using AdoAsync.BulkCopy.LinqToDb.Typed;
using AdoAsync.Extensions.Execution;
using AdoAsync.Helpers;
using AdoAsync.Resilience;
using AdoAsync.Validation;
using FluentValidation;
using Polly;

namespace AdoAsync.Execution;

/// <summary>
/// Orchestrates validation → resilience → provider → ADO.NET. Not thread-safe. Streaming by default; materialization is explicit and allocation-heavy. Retries are Polly-based and opt-in; cancellation is honored on all async calls; transactions are explicit via the transaction manager.
/// </summary>
public sealed partial class DbExecutor : IDbExecutor
{
    private static readonly IReadOnlyDictionary<string, object?> EmptyOutputParameters =
        new ReadOnlyDictionary<string, object?>(new Dictionary<string, object?>());

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

    /// <summary>Creates a new executor for the specified options.</summary>
    public static DbExecutor Create(DbOptions options, bool isInUserTransaction = false)
    {
        try
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
                throw new DbCallerException(validationError);
            }

            return new DbExecutor(options, provider, retryPolicy, commandValidator, parameterValidator, bulkImportValidator, linqToDbBulkImporter);
        }
        catch (Exception ex)
        {
            if (ex is DbCallerException)
            {
                throw;
            }

            throw new DbCallerException(DbErrorMapper.Map(ex), ex);
        }
    }

    #region Public API - Streaming
    /// <summary>Execute a single SELECT and return a streaming reader result (SQL Server/PostgreSQL only).</summary>
    public async ValueTask<StreamingReaderResult> ExecuteReaderAsync(CommandDefinition command, CancellationToken cancellationToken = default)
    {
        var declaredOutputParameters = ParameterHelper.HasNonRefCursorOutputs(command.Parameters)
            ? command.Parameters
            : null;
        return await ExecuteReaderCoreAsync(command, declaredOutputParameters, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Stream rows for a single SELECT.</summary>
    public async IAsyncEnumerable<IDataRecord> StreamAsync(CommandDefinition command, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (_options.DatabaseType == DatabaseType.Oracle)
        {
            throw new DbCallerException(DbErrorMapper.Map(new DatabaseException(
                ErrorCategory.Unsupported,
                "Streaming is not supported for Oracle in StreamAsync. Use buffered materialization instead.")));
        }

        await using var result = await ExecuteReaderAsync(command, cancellationToken).ConfigureAwait(false);
        await foreach (var record in result.Reader.StreamRecordsAsync(cancellationToken).ConfigureAwait(false))
        {
            yield return record;
        }
    }
    #endregion

    private async ValueTask<StreamingReaderResult> ExecuteReaderCoreAsync(
        CommandDefinition command,
        IReadOnlyList<DbParameter>? declaredOutputParameters,
        CancellationToken cancellationToken)
    {
        var validationError = ValidateCommandDefinition(command);
        if (validationError is not null)
        {
            throw new DbCallerException(validationError);
        }

        if (_options.DatabaseType == DatabaseType.Oracle)
        {
            throw new DbCallerException(DbErrorMapper.Map(new DatabaseException(
                ErrorCategory.Unsupported,
                "Oracle does not support streaming. Use buffered materialization instead.")));
        }

        if (CursorHelper.IsPostgresRefCursor(_options.DatabaseType, command))
        {
            throw new DbCallerException(DbErrorMapper.Map(new DatabaseException(ErrorCategory.Unsupported, "PostgreSQL refcursor requires buffered materialization (QueryTableAsync/ExecuteDataSetAsync).")));
        }

        DbCommand? dbCommand = null;
        try
        {
            dbCommand = await CreateCommandAsync(command, cancellationToken).ConfigureAwait(false);
            var reader = await dbCommand.ExecuteReaderAsync(command.Behavior, cancellationToken).ConfigureAwait(false);
            return new StreamingReaderResult(dbCommand, reader, declaredOutputParameters);
        }
        catch (Exception ex)
        {
            await DisposeCommandAsync(dbCommand).ConfigureAwait(false);
            throw WrapException(ex);
        }
    }

    #region Public API - Execute
    /// <summary>Execute a command and return row count plus output parameters.</summary>
    public async ValueTask<(int RowsAffected, IReadOnlyDictionary<string, object?> OutputParameters)> ExecuteAsync(
        CommandDefinition command,
        CancellationToken cancellationToken = default)
    {
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
                var rows = await dbCommand.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
                var outputs = ExtractOutputParametersOrEmpty(dbCommand, command.Parameters);
                return (RowsAffected: rows, OutputParameters: outputs);
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            throw WrapException(ex);
        }
    }

    /// <summary>Execute a scalar command and return value plus output parameters.</summary>
    public async ValueTask<(T Value, IReadOnlyDictionary<string, object?> OutputParameters)> ExecuteScalarAsync<T>(
        CommandDefinition command,
        CancellationToken cancellationToken = default)
    {
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
                var value = await dbCommand.ExecuteScalarAsync(ct).ConfigureAwait(false);
                var outputs = ExtractOutputParametersOrEmpty(dbCommand, command.Parameters);
                if (value is null or DBNull)
                {
                    return (Value: default!, OutputParameters: outputs);
                }

                if (value is T typed)
                {
                    return (Value: typed, OutputParameters: outputs);
                }

                var converted = (T)Convert.ChangeType(value, typeof(T));
                return (Value: converted, OutputParameters: outputs);
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            throw WrapException(ex);
        }
    }
    #endregion

    #region Public API - Buffered Query
    /// <summary>Buffered single result as DataTable.</summary>
    public async ValueTask<(DataTable Table, IReadOnlyDictionary<string, object?> OutputParameters)> QueryTableAsync(
        CommandDefinition command,
        CancellationToken cancellationToken = default)
    {
        var validationError = ValidateCommandDefinition(command);
        if (validationError is not null)
        {
            throw new DbCallerException(validationError);
        }

        if (IsRefCursorCommand(command))
        {
            var (tables, outputs) = await ExecuteRefCursorsWithOutputsAsync(command, cancellationToken).ConfigureAwait(false);
            if (tables is { Count: > 0 })
            {
                var table = tables[0];

                // QueryTableAsync returns a single table; dispose any additional tables to avoid leaks.
                for (var i = 1; i < tables.Count; i++)
                {
                    tables[i].Dispose();
                }

                return (Table: table, OutputParameters: outputs);
            }

            var empty = new DataTable();
            return (Table: empty, OutputParameters: outputs);
        }

        try
        {
            return await ExecuteWithRetryIfAllowedAsync(async ct =>
            {
                await using var dbCommand = await CreateCommandAsync(command, ct).ConfigureAwait(false);
                var table = await DataAdapterHelper.FillTableAsync(dbCommand, ct).ConfigureAwait(false);
                var outputs = ExtractOutputParametersOrEmpty(dbCommand, command.Parameters);
                return (Table: table, OutputParameters: outputs);
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            throw WrapException(ex);
        }
    }

    /// <summary>Materialize result sets into buffered tables.</summary>
    public async ValueTask<(IReadOnlyList<DataTable> Tables, IReadOnlyDictionary<string, object?> OutputParameters)> QueryTablesAsync(
        CommandDefinition command,
        CancellationToken cancellationToken = default)
    {
        var validationError = ValidateCommandDefinition(command);
        if (validationError is not null)
        {
            throw new DbCallerException(validationError);
        }

        if (IsRefCursorCommand(command))
        {
            return await ExecuteRefCursorsWithOutputsAsync(command, cancellationToken).ConfigureAwait(false);
        }

        try
        {
            return await ExecuteWithRetryIfAllowedAsync(async ct =>
            {
                await using var dbCommand = await CreateCommandAsync(command, ct).ConfigureAwait(false);
                var tables = await DataAdapterHelper.FillTablesAsync(dbCommand, ct).ConfigureAwait(false);
                var outputs = ExtractOutputParametersOrEmpty(dbCommand, command.Parameters);
                return (Tables: (IReadOnlyList<DataTable>)tables, OutputParameters: outputs);
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            var error = MapError(ex);
            throw new DbCallerException(error, ex);
        }
    }

    /// <summary>Buffer rows with an explicit mapper.</summary>
    public async ValueTask<(List<T> Rows, IReadOnlyDictionary<string, object?> OutputParameters)> QueryAsync<T>(
        CommandDefinition command,
        Func<DataRow, T> map,
        CancellationToken cancellationToken = default)
    {
        Validate.Required(map, nameof(map));
        var validationError = ValidateCommandDefinition(command);
        if (validationError is not null)
        {
            throw new DbCallerException(validationError);
        }

        try
        {
            var (table, outputs) = await QueryTableAsync(command, cancellationToken).ConfigureAwait(false);
            try
            {
                return (Rows: table.ToList(map), OutputParameters: outputs);
            }
            finally
            {
                table.Dispose();
            }
        }
        catch (Exception ex)
        {
            throw WrapException(ex);
        }
    }
    #endregion

    #region Public API - DataSet
    /// <summary>Buffered multi-result as DataSet.</summary>
    public async ValueTask<(DataSet DataSet, IReadOnlyDictionary<string, object?> OutputParameters)> ExecuteDataSetAsync(
        CommandDefinition command,
        CancellationToken cancellationToken = default)
    {
        var validationError = ValidateCommandDefinition(command);
        if (validationError is not null)
        {
            throw new DbCallerException(validationError);
        }

        if (IsRefCursorCommand(command))
        {
            var cursorResult = await ExecuteRefCursorsWithOutputsAsync(command, cancellationToken).ConfigureAwait(false);
            var refCursorDataSet = new DataSet();
            foreach (var table in cursorResult.Tables)
            {
                refCursorDataSet.Tables.Add(table);
            }

            return (DataSet: refCursorDataSet, OutputParameters: cursorResult.OutputParameters);
        }

        var (dataSet, outputs) = await ExecuteDataSetInternalAsync(command, cancellationToken).ConfigureAwait(false);
        return (DataSet: dataSet, OutputParameters: outputs ?? EmptyOutputParameters);
    }
    #endregion

    #region Private - DataSet
    private async ValueTask<(DataSet DataSet, IReadOnlyDictionary<string, object?>? OutputParameters)> ExecuteDataSetInternalAsync(
        CommandDefinition command,
        CancellationToken cancellationToken)
    {
        try
        {
            return await ExecuteWithRetryIfAllowedAsync(async ct =>
            {
                await using var dbCommand = await CreateCommandAsync(command, ct).ConfigureAwait(false);

                var dataSet = await DataAdapterHelper.FillDataSetAsync(dbCommand, ct).ConfigureAwait(false);
                var outputs = ParameterHelper.ExtractOutputParameters(dbCommand, command.Parameters);
                return (dataSet, outputs);
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            throw WrapException(ex);
        }
    }
    #endregion
}
