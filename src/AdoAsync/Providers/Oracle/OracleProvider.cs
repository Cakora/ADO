using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Oracle.ManagedDataAccess.Client;
using AdoAsync.Abstractions;

namespace AdoAsync.Providers.Oracle;

/// <summary>
/// Oracle provider implementation (parameter mapping and error translation).
/// </summary>
public sealed class OracleProvider : IDbProvider
{
    #region Public API
    /// <summary>Creates an Oracle connection.</summary>
    public DbConnection CreateConnection(string connectionString)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        return new OracleConnection(connectionString);
    }

    /// <summary>Creates an Oracle command.</summary>
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

    /// <summary>Applies parameters to an Oracle command (cursor handling must be explicit).</summary>
    public void ApplyParameters(DbCommand command, IEnumerable<DbParameter> parameters)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(parameters);

        // Oracle cursor outputs must be defined provider-side; map cursor parameters explicitly when needed.
        foreach (var param in parameters)
        {
            var oraParam = new OracleParameter
            {
                ParameterName = param.Name,
                Value = param.Value ?? DBNull.Value,
                Direction = param.Direction
            };

            if (param.Size.HasValue)
            {
                oraParam.Size = param.Size.Value;
            }

            if (param.Precision.HasValue)
            {
                oraParam.Precision = param.Precision.Value;
            }

            if (param.Scale.HasValue)
            {
                oraParam.Scale = param.Scale.Value;
            }

            oraParam.OracleDbType = OracleTypeMapper.Map(param.DataType);
            command.Parameters.Add(oraParam);
        }
    }

    /// <summary>Performs Oracle bulk import via OracleBulkCopy.</summary>
    public ValueTask<int> BulkImportAsync(DbConnection connection, BulkImportRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(request);

        if (connection is not OracleConnection oracleConnection)
        {
            throw new InvalidOperationException("Oracle bulk import requires an OracleConnection.");
        }

        using var bulkCopy = new OracleBulkCopy(oracleConnection)
        {
            DestinationTableName = request.DestinationTable,
            NotifyAfter = 1
        };

        if (request.BatchSize.HasValue)
        {
            bulkCopy.BatchSize = request.BatchSize.Value;
        }

        var rowsCopied = 0;
        bulkCopy.OracleRowsCopied += (_, args) => rowsCopied = (int)args.RowsCopied;

        foreach (var mapping in request.ColumnMappings)
        {
            bulkCopy.ColumnMappings.Add(mapping.SourceColumn, mapping.DestinationColumn);
        }

        cancellationToken.ThrowIfCancellationRequested();
        bulkCopy.WriteToServer(request.SourceReader);
        return new ValueTask<int>(rowsCopied);
    }
    #endregion
}
