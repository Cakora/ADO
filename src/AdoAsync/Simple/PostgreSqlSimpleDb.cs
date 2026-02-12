using System;
using System.Collections.Generic;
using System.Data;
using Npgsql;
using AdoAsync.Helpers;
using AdoAsync.Providers.PostgreSql;

namespace AdoAsync.Simple;

/// <summary>Simple PostgreSQL implementation for returning DataTable + outputs.</summary>
public sealed class PostgreSqlSimpleDb : IDisposable
{
    private readonly string _connectionString;

    /// <summary>Create a new PostgreSQL helper.</summary>
    public PostgreSqlSimpleDb(string connectionString)
    {
        _connectionString = connectionString;
    }

    /// <summary>Dispose resources (no-op; connections are per-call).</summary>
    public void Dispose()
    {
    }

    /// <summary>Execute a command and return a single DataTable plus output parameters.</summary>
    public async Task<(DataTable Table, IReadOnlyDictionary<string, object?> OutputParameters)> QueryTableAsync(
        string commandText,
        CommandType commandType = CommandType.StoredProcedure,
        IEnumerable<SimpleParameter>? parameters = null,
        CommonProcessInput? common = null,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(ResolveConnectionString(common));
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = CreateCommand(connection, commandText, commandType, common?.CommandTimeoutSeconds);
        var parameterList = AddParameters(command, commandType, parameters);

        using var adapter = new NpgsqlDataAdapter(command);
        var table = new DataTable();
        adapter.Fill(table);

        var outputs = ExtractOutputs(command, parameterList);
        return (Table: table, OutputParameters: outputs);
    }

    /// <summary>Execute a command inside its own transaction and return a single DataTable plus output parameters.</summary>
    public async Task<(DataTable Table, IReadOnlyDictionary<string, object?> OutputParameters)> QueryTableInTransactionAsync(
        string commandText,
        CommandType commandType = CommandType.StoredProcedure,
        IEnumerable<SimpleParameter>? parameters = null,
        CommonProcessInput? common = null,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(ResolveConnectionString(common));
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            await using var command = CreateCommand(connection, commandText, commandType, common?.CommandTimeoutSeconds);
            command.Transaction = transaction;
            var parameterList = AddParameters(command, commandType, parameters);

            using var adapter = new NpgsqlDataAdapter(command);
            var table = new DataTable();
            adapter.Fill(table);

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            var outputs = ExtractOutputs(command, parameterList);
            return (Table: table, OutputParameters: outputs);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>Execute a scalar command and return value plus output parameters.</summary>
    public async Task<(T Value, IReadOnlyDictionary<string, object?> OutputParameters)> ExecuteScalarAsync<T>(
        string commandText,
        CommandType commandType = CommandType.StoredProcedure,
        IEnumerable<SimpleParameter>? parameters = null,
        CommonProcessInput? common = null,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(ResolveConnectionString(common));
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = CreateCommand(connection, commandText, commandType, common?.CommandTimeoutSeconds);
        var parameterList = AddParameters(command, commandType, parameters);

        var value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        var outputs = ExtractOutputs(command, parameterList);
        if (value is null or DBNull)
        {
            return (Value: default!, OutputParameters: outputs);
        }

        if (value is T typed)
        {
            return (Value: typed, OutputParameters: outputs);
        }

        return (Value: (T)Convert.ChangeType(value, typeof(T)), OutputParameters: outputs);
    }

    /// <summary>Execute a scalar command inside its own transaction and return value plus output parameters.</summary>
    public async Task<(T Value, IReadOnlyDictionary<string, object?> OutputParameters)> ExecuteScalarInTransactionAsync<T>(
        string commandText,
        CommandType commandType = CommandType.StoredProcedure,
        IEnumerable<SimpleParameter>? parameters = null,
        CommonProcessInput? common = null,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(ResolveConnectionString(common));
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            await using var command = CreateCommand(connection, commandText, commandType, common?.CommandTimeoutSeconds);
            command.Transaction = transaction;
            var parameterList = AddParameters(command, commandType, parameters);

            var value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            var outputs = ExtractOutputs(command, parameterList);

            if (value is null or DBNull)
            {
                return (Value: default!, OutputParameters: outputs);
            }

            if (value is T typed)
            {
                return (Value: typed, OutputParameters: outputs);
            }

            return (Value: (T)Convert.ChangeType(value, typeof(T)), OutputParameters: outputs);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>Execute a non-query command and return affected rows plus output parameters.</summary>
    public async Task<(int RowsAffected, IReadOnlyDictionary<string, object?> OutputParameters)> ExecuteNonQueryAsync(
        string commandText,
        CommandType commandType = CommandType.StoredProcedure,
        IEnumerable<SimpleParameter>? parameters = null,
        CommonProcessInput? common = null,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(ResolveConnectionString(common));
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = CreateCommand(connection, commandText, commandType, common?.CommandTimeoutSeconds);
        var parameterList = AddParameters(command, commandType, parameters);

        var rows = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        var outputs = ExtractOutputs(command, parameterList);
        return (RowsAffected: rows, OutputParameters: outputs);
    }

    /// <summary>Execute a non-query command inside its own transaction and return affected rows plus output parameters.</summary>
    public async Task<(int RowsAffected, IReadOnlyDictionary<string, object?> OutputParameters)> ExecuteNonQueryInTransactionAsync(
        string commandText,
        CommandType commandType = CommandType.StoredProcedure,
        IEnumerable<SimpleParameter>? parameters = null,
        CommonProcessInput? common = null,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(ResolveConnectionString(common));
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            await using var command = CreateCommand(connection, commandText, commandType, common?.CommandTimeoutSeconds);
            command.Transaction = transaction;
            var parameterList = AddParameters(command, commandType, parameters);

            var rows = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            var outputs = ExtractOutputs(command, parameterList);
            return (RowsAffected: rows, OutputParameters: outputs);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>Execute a command and return DataSet plus output parameters.</summary>
    public async Task<(DataSet DataSet, IReadOnlyDictionary<string, object?> OutputParameters)> ExecuteDataSetAsync(
        string commandText,
        CommandType commandType = CommandType.StoredProcedure,
        IEnumerable<SimpleParameter>? parameters = null,
        CommonProcessInput? common = null,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(ResolveConnectionString(common));
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = CreateCommand(connection, commandText, commandType, common?.CommandTimeoutSeconds);
        var parameterList = AddParameters(command, commandType, parameters);

        using var adapter = new NpgsqlDataAdapter(command);
        var dataSet = new DataSet();
        adapter.Fill(dataSet);

        var outputs = ExtractOutputs(command, parameterList);
        return (DataSet: dataSet, OutputParameters: outputs);
    }

    /// <summary>Execute a command inside its own transaction and return DataSet plus output parameters.</summary>
    public async Task<(DataSet DataSet, IReadOnlyDictionary<string, object?> OutputParameters)> ExecuteDataSetInTransactionAsync(
        string commandText,
        CommandType commandType = CommandType.StoredProcedure,
        IEnumerable<SimpleParameter>? parameters = null,
        CommonProcessInput? common = null,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(ResolveConnectionString(common));
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            await using var command = CreateCommand(connection, commandText, commandType, common?.CommandTimeoutSeconds);
            command.Transaction = transaction;
            var parameterList = AddParameters(command, commandType, parameters);

            using var adapter = new NpgsqlDataAdapter(command);
            var dataSet = new DataSet();
            adapter.Fill(dataSet);

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            var outputs = ExtractOutputs(command, parameterList);
            return (DataSet: dataSet, OutputParameters: outputs);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    private static NpgsqlCommand CreateCommand(NpgsqlConnection connection, string commandText, CommandType commandType, int? commandTimeoutSeconds)
    {
        var command = connection.CreateCommand();
        command.CommandText = commandText;
        command.CommandType = commandType;
        if (commandTimeoutSeconds.HasValue)
        {
            command.CommandTimeout = commandTimeoutSeconds.Value;
        }
        return command;
    }

    private static IReadOnlyList<SimpleParameter>? AddParameters(NpgsqlCommand command, CommandType commandType, IEnumerable<SimpleParameter>? parameters)
    {
        if (parameters is null)
        {
            return null;
        }

        var list = parameters as List<SimpleParameter> ?? new List<SimpleParameter>(parameters);
        ValidateRefCursorUsage(commandType, list);
        foreach (var parameter in list)
        {
            if (parameter.Direction != ParameterDirection.Input && parameter.DataType is null)
            {
                throw new DatabaseException(ErrorCategory.Validation, $"Output parameters must specify DataType. Name='{parameter.Name}'.");
            }

            var name = NormalizeParameterName(parameter.Name);
            var pgParameter = new NpgsqlParameter
            {
                ParameterName = name,
                Direction = parameter.Direction,
                Value = parameter.Value ?? DBNull.Value
            };

            if (parameter.DataType.HasValue)
            {
                pgParameter.NpgsqlDbType = PostgreSqlTypeMapper.Map(parameter.DataType.Value);
            }

            if (parameter.Size.HasValue)
            {
                pgParameter.Size = parameter.Size.Value;
            }

            command.Parameters.Add(pgParameter);
        }

        return list;
    }

    private static IReadOnlyDictionary<string, object?> ExtractOutputs(NpgsqlCommand command, IReadOnlyList<SimpleParameter>? parameters)
    {
        if (parameters is null || parameters.Count == 0)
        {
            return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        }

        var outputLookup = new Dictionary<string, DbDataType>(StringComparer.OrdinalIgnoreCase);
        foreach (var output in parameters)
        {
            if (output.Direction == ParameterDirection.Input || output.DataType is null)
            {
                continue;
            }

            outputLookup[ParameterHelper.TrimParameterPrefix(output.Name)] = output.DataType.Value;
        }

        var values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (NpgsqlParameter param in command.Parameters)
        {
            if (param.Direction == ParameterDirection.Input)
            {
                continue;
            }

            var key = ParameterHelper.TrimParameterPrefix(param.ParameterName);
            if (outputLookup.TryGetValue(key, out var type) && type == DbDataType.RefCursor)
            {
                continue;
            }

            values[key] = param.Value is DBNull ? null : param.Value;
        }

        return values;
    }

    private string ResolveConnectionString(CommonProcessInput? common)
    {
        if (common is null)
        {
            return _connectionString;
        }

        if (string.IsNullOrWhiteSpace(common.ConnectionString))
        {
            throw new ArgumentException("CommonProcessInput.ConnectionString is required.", nameof(common));
        }

        return common.ConnectionString;
    }

    private static string NormalizeParameterName(string name)
        => ParameterHelper.TrimParameterPrefix(name);

    private static void ValidateRefCursorUsage(CommandType commandType, IReadOnlyList<SimpleParameter> parameters)
    {
        if (commandType != CommandType.StoredProcedure)
        {
            foreach (var parameter in parameters)
            {
                if (parameter.DataType == DbDataType.RefCursor)
                {
                    throw new DatabaseException(ErrorCategory.Validation, "PostgreSQL refcursor outputs require CommandType.StoredProcedure.");
                }
            }
        }
    }
}
