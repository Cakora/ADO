using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.Data.SqlClient;
using AdoAsync.Helpers;
using AdoAsync.Providers.SqlServer;

namespace AdoAsync.Simple;

/// <summary>Simple SQL Server implementation for returning DataTable + outputs.</summary>
public sealed class SqlServerSimpleDb : IDisposable
{
    private readonly string _connectionString;

    /// <summary>Create a new SQL Server helper.</summary>
    public SqlServerSimpleDb(string connectionString)
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
        CommandType commandType,
        IEnumerable<SimpleParameter>? parameters = null,
        int? commandTimeoutSeconds = null,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = CreateCommand(connection, commandText, commandType, commandTimeoutSeconds);
        var parameterList = AddParameters(command, parameters);

        using var adapter = new SqlDataAdapter(command);
        var table = new DataTable();
        adapter.Fill(table);

        var outputs = ExtractOutputs(command, parameterList);
        return (Table: table, OutputParameters: outputs);
    }

    /// <summary>Execute a command inside its own transaction and return a single DataTable plus output parameters.</summary>
    public async Task<(DataTable Table, IReadOnlyDictionary<string, object?> OutputParameters)> QueryTableInTransactionAsync(
        string commandText,
        CommandType commandType,
        IEnumerable<SimpleParameter>? parameters = null,
        int? commandTimeoutSeconds = null,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            await using var command = CreateCommand(connection, commandText, commandType, commandTimeoutSeconds);
            command.Transaction = transaction;
            var parameterList = AddParameters(command, parameters);

            using var adapter = new SqlDataAdapter(command);
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
        CommandType commandType,
        IEnumerable<SimpleParameter>? parameters = null,
        int? commandTimeoutSeconds = null,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = CreateCommand(connection, commandText, commandType, commandTimeoutSeconds);
        var parameterList = AddParameters(command, parameters);

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
        CommandType commandType,
        IEnumerable<SimpleParameter>? parameters = null,
        int? commandTimeoutSeconds = null,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            await using var command = CreateCommand(connection, commandText, commandType, commandTimeoutSeconds);
            command.Transaction = transaction;
            var parameterList = AddParameters(command, parameters);

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
        CommandType commandType,
        IEnumerable<SimpleParameter>? parameters = null,
        int? commandTimeoutSeconds = null,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = CreateCommand(connection, commandText, commandType, commandTimeoutSeconds);
        var parameterList = AddParameters(command, parameters);

        var rows = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        var outputs = ExtractOutputs(command, parameterList);
        return (RowsAffected: rows, OutputParameters: outputs);
    }

    /// <summary>Execute a non-query command inside its own transaction and return affected rows plus output parameters.</summary>
    public async Task<(int RowsAffected, IReadOnlyDictionary<string, object?> OutputParameters)> ExecuteNonQueryInTransactionAsync(
        string commandText,
        CommandType commandType,
        IEnumerable<SimpleParameter>? parameters = null,
        int? commandTimeoutSeconds = null,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            await using var command = CreateCommand(connection, commandText, commandType, commandTimeoutSeconds);
            command.Transaction = transaction;
            var parameterList = AddParameters(command, parameters);

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
        CommandType commandType,
        IEnumerable<SimpleParameter>? parameters = null,
        int? commandTimeoutSeconds = null,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = CreateCommand(connection, commandText, commandType, commandTimeoutSeconds);
        var parameterList = AddParameters(command, parameters);

        using var adapter = new SqlDataAdapter(command);
        var dataSet = new DataSet();
        adapter.Fill(dataSet);

        var outputs = ExtractOutputs(command, parameterList);
        return (DataSet: dataSet, OutputParameters: outputs);
    }

    /// <summary>Execute a command inside its own transaction and return DataSet plus output parameters.</summary>
    public async Task<(DataSet DataSet, IReadOnlyDictionary<string, object?> OutputParameters)> ExecuteDataSetInTransactionAsync(
        string commandText,
        CommandType commandType,
        IEnumerable<SimpleParameter>? parameters = null,
        int? commandTimeoutSeconds = null,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            await using var command = CreateCommand(connection, commandText, commandType, commandTimeoutSeconds);
            command.Transaction = transaction;
            var parameterList = AddParameters(command, parameters);

            using var adapter = new SqlDataAdapter(command);
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

    /// <summary>Begin a transaction for multiple commands on the same connection.</summary>
    public async Task<(SqlConnection Connection, SqlTransaction Transaction)> BeginTransactionAsync(
        CancellationToken cancellationToken = default)
    {
        var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        return (Connection: connection, Transaction: transaction);
    }

    /// <summary>Execute a non-query command using an existing transaction and return affected rows plus output parameters.</summary>
    public async Task<(int RowsAffected, IReadOnlyDictionary<string, object?> OutputParameters)> ExecuteNonQueryAsync(
        string commandText,
        CommandType commandType,
        SqlTransaction transaction,
        IEnumerable<SimpleParameter>? parameters = null,
        int? commandTimeoutSeconds = null,
        CancellationToken cancellationToken = default)
    {
        var connection = GetTransactionConnection(transaction);
        await using var command = CreateCommand(connection, commandText, commandType, commandTimeoutSeconds);
        command.Transaction = transaction;
        var parameterList = AddParameters(command, parameters);

        var rows = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        var outputs = ExtractOutputs(command, parameterList);
        return (RowsAffected: rows, OutputParameters: outputs);
    }

    private static SqlCommand CreateCommand(SqlConnection connection, string commandText, CommandType commandType, int? commandTimeoutSeconds)
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

    private static IReadOnlyList<SimpleParameter>? AddParameters(SqlCommand command, IEnumerable<SimpleParameter>? parameters)
    {
        if (parameters is null)
        {
            return null;
        }

        var list = parameters as List<SimpleParameter> ?? new List<SimpleParameter>(parameters);
        ValidateRefCursorUsage(list);
        foreach (var parameter in list)
        {
            if (parameter.Direction != ParameterDirection.Input && parameter.DataType is null)
            {
                throw new DatabaseException(ErrorCategory.Validation, $"Output parameters must specify DataType. Name='{parameter.Name}'.");
            }

            var name = NormalizeParameterName(parameter.Name);
            var sqlParameter = new SqlParameter
            {
                ParameterName = name,
                Direction = parameter.Direction,
                Value = parameter.Value ?? DBNull.Value
            };

            if (parameter.DataType.HasValue)
            {
                sqlParameter.SqlDbType = SqlServerTypeMapper.Map(parameter.DataType.Value);
            }

            if (parameter.Size.HasValue)
            {
                sqlParameter.Size = parameter.Size.Value;
            }

            command.Parameters.Add(sqlParameter);
        }

        return list;
    }

    private static IReadOnlyDictionary<string, object?> ExtractOutputs(SqlCommand command, IReadOnlyList<SimpleParameter>? parameters)
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
        foreach (SqlParameter param in command.Parameters)
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

    private static string NormalizeParameterName(string name)
        => "@" + ParameterHelper.TrimParameterPrefix(name);

    private static void ValidateRefCursorUsage(IReadOnlyList<SimpleParameter> parameters)
    {
        foreach (var parameter in parameters)
        {
            if (parameter.DataType == DbDataType.RefCursor)
            {
                throw new DatabaseException(ErrorCategory.Validation, "SQL Server does not support RefCursor parameters.");
            }
        }
    }

    private static SqlConnection GetTransactionConnection(SqlTransaction transaction)
        => transaction.Connection ?? throw new ArgumentException("Transaction must have an open connection.", nameof(transaction));
}
