using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Npgsql;
using AdoAsync.Abstractions;

namespace AdoAsync.Providers.PostgreSql;

/// <summary>
/// PostgreSQL provider implementation (parameter mapping and error translation).
/// </summary>
public sealed class PostgreSqlProvider : IDbProvider
{
    #region Public API
    /// <summary>Creates a PostgreSQL connection.</summary>
    public DbConnection CreateConnection(string connectionString)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        return new NpgsqlConnection(connectionString);
    }

    /// <summary>Creates a PostgreSQL command.</summary>
    public DbCommand CreateCommand(DbConnection connection, CommandDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(definition);

        var command = connection.CreateCommand();
        command.CommandText = definition.CommandText;
        command.CommandType = definition.CommandType;
        if (definition.CommandTimeoutSeconds.HasValue)
        {
            command.CommandTimeout = definition.CommandTimeoutSeconds.Value;
        }
        return command;
    }

    /// <summary>Applies parameters to a PostgreSQL command.</summary>
    public void ApplyParameters(DbCommand command, IEnumerable<DbParameter> parameters)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(parameters);

        foreach (var param in parameters)
        {
            var npgParam = new NpgsqlParameter
            {
                ParameterName = param.Name,
                Value = param.Value ?? DBNull.Value,
                Direction = param.Direction
            };

            if (param.Size.HasValue)
            {
                npgParam.Size = param.Size.Value;
            }

            if (param.Precision.HasValue)
            {
                npgParam.Precision = param.Precision.Value;
            }

            if (param.Scale.HasValue)
            {
                npgParam.Scale = param.Scale.Value;
            }

            npgParam.NpgsqlDbType = PostgreSqlTypeMapper.Map(param.DataType);
            command.Parameters.Add(npgParam);
        }
    }

    /// <summary>Performs PostgreSQL bulk import via COPY binary.</summary>
    public async ValueTask<int> BulkImportAsync(DbConnection connection, BulkImportRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(request);

        if (connection is not NpgsqlConnection npgConnection)
        {
            throw new InvalidOperationException("PostgreSQL bulk import requires an NpgsqlConnection.");
        }

        var copyCommand = BuildCopyCommand(request);
        await using var importer = npgConnection.BeginBinaryImport(copyCommand);

        var ordinals = new int[request.ColumnMappings.Count];
        for (var i = 0; i < request.ColumnMappings.Count; i++)
        {
            ordinals[i] = request.SourceReader.GetOrdinal(request.ColumnMappings[i].SourceColumn);
        }

        var rows = 0;
        while (await request.SourceReader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            cancellationToken.ThrowIfCancellationRequested();
            await importer.StartRowAsync(cancellationToken).ConfigureAwait(false);

            for (var i = 0; i < ordinals.Length; i++)
            {
                var value = request.SourceReader.GetValue(ordinals[i]);
                if (value is null || value is DBNull)
                {
                    await importer.WriteNullAsync(cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    await importer.WriteAsync(value, cancellationToken).ConfigureAwait(false);
                }
            }

            rows++;
        }

        await importer.CompleteAsync(cancellationToken).ConfigureAwait(false);
        return rows;
    }
    #endregion

    #region Private Helpers
    private static string BuildCopyCommand(BulkImportRequest request)
    {
        var tableName = QuoteIdentifier(request.DestinationTable);
        var columns = new string[request.ColumnMappings.Count];
        for (var i = 0; i < request.ColumnMappings.Count; i++)
        {
            columns[i] = QuoteIdentifier(request.ColumnMappings[i].DestinationColumn);
        }

        var columnList = string.Join(", ", columns);
        return $"COPY {tableName} ({columnList}) FROM STDIN (FORMAT BINARY)";
    }

    private static string QuoteIdentifier(string identifier)
    {
        var parts = identifier.Split('.');
        for (var i = 0; i < parts.Length; i++)
        {
            parts[i] = $"\"{parts[i].Replace("\"", "\"\"")}\"";
        }

        return string.Join(".", parts);
    }
    #endregion
}
