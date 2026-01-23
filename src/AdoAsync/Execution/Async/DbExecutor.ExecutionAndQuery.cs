using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using AdoAsync.Abstractions;
using AdoAsync.Extensions.Execution;
using AdoAsync.Helpers;

namespace AdoAsync.Execution;

public sealed partial class DbExecutor
{
    #region Public API - Streaming
    /// <summary>Execute a single SELECT and return a streaming reader.</summary>
    public async ValueTask<DbDataReader> ExecuteReaderAsync(CommandDefinition command, CancellationToken cancellationToken = default)
    {
        await EnsureNotDisposedAsync().ConfigureAwait(false);
        var validationError = ValidateCommandDefinition(command);
        if (validationError is not null)
        {
            throw new DbCallerException(validationError);
        }

        try
        {
            var dbCommand = await CreateCommandAsync(command, cancellationToken).ConfigureAwait(false);
            var reader = await dbCommand.ExecuteReaderAsync(command.Behavior, cancellationToken).ConfigureAwait(false);
            return new CommandOwningDbDataReader(dbCommand, reader);
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
            throw new DatabaseException(ErrorCategory.Unsupported, "Streaming is not supported for Oracle in StreamAsync. Use buffered materialization instead.");
        }

        await using var reader = await ExecuteReaderAsync(command, cancellationToken).ConfigureAwait(false);
        await foreach (var record in reader.StreamRecordsAsync(cancellationToken).ConfigureAwait(false))
        {
            yield return record;
        }
    }
    #endregion

    #region Public API - Execute
    /// <summary>Execute a command and return row count plus output parameters.</summary>
    public async ValueTask<(int RowsAffected, IReadOnlyDictionary<string, object?> OutputParameters)> ExecuteAsync(
        CommandDefinition command,
        CancellationToken cancellationToken = default)
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
        await EnsureNotDisposedAsync().ConfigureAwait(false);
        var validationError = ValidateCommandDefinition(command);
        if (validationError is not null)
        {
            throw new DbCallerException(validationError);
        }

        if (IsRefCursorCommand(command))
        {
            var (tables, outputs) = await ExecuteRefCursorsWithOutputsAsync(command, cancellationToken).ConfigureAwait(false);
            var table = tables is { Count: > 0 } ? tables[0] : new DataTable();
            return (Table: table, OutputParameters: outputs);
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
        await EnsureNotDisposedAsync().ConfigureAwait(false);
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
            return (Rows: table.ToList(map), OutputParameters: outputs);
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
        await EnsureNotDisposedAsync().ConfigureAwait(false);
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
