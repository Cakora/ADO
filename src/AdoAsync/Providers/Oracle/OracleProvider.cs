using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Oracle.ManagedDataAccess.Client;
using Oracle.ManagedDataAccess.Types;
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
        Validate.Required(connectionString, nameof(connectionString));
        return new OracleConnection(connectionString);
    }

    /// <summary>Creates an Oracle command.</summary>
    public DbCommand CreateCommand(DbConnection connection, CommandDefinition definition)
    {
        Validate.Required(connection, nameof(connection));
        Validate.Required(definition, nameof(definition));

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
        Validate.Required(command, nameof(command));
        Validate.Required(parameters, nameof(parameters));

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
    public ValueTask<int> BulkImportAsync(DbConnection connection, DbTransaction? transaction, BulkImportRequest request, CancellationToken cancellationToken = default)
    {
        Validate.Required(connection, nameof(connection));
        Validate.Required(request, nameof(request));

        if (connection is not OracleConnection oracleConnection)
        {
            throw new DatabaseException(ErrorCategory.Configuration, "Oracle bulk import requires an OracleConnection.");
        }

        if (transaction is not null && transaction is not OracleTransaction)
        {
            throw new DatabaseException(ErrorCategory.Configuration, "Oracle bulk import requires an OracleTransaction when a transaction is provided.");
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
            // Explicit mapping avoids column order assumptions.
            bulkCopy.ColumnMappings.Add(mapping.SourceColumn, mapping.DestinationColumn);
        }

        // Oracle bulk copy is synchronous; check cancellation before the call.
        cancellationToken.ThrowIfCancellationRequested();
        bulkCopy.WriteToServer(request.SourceReader);
        return new ValueTask<int>(rowsCopied);
    }
    #endregion

    #region Internal Helpers
    internal static IReadOnlyList<DataTable> ReadRefCursorResults(DbCommand command)
    {
        #region Refcursor
        var tables = new List<DataTable>();
        // Oracle exposes result sets via output refcursor parameters.
        foreach (OracleParameter parameter in command.Parameters)
        {
            if (parameter.OracleDbType != OracleDbType.RefCursor)
            {
                continue;
            }

            if (parameter.Value is not OracleRefCursor cursor)
            {
                continue;
            }

            using var reader = cursor.GetDataReader();
            var table = new DataTable();
            table.Load(reader);
            tables.Add(table);
        }

        return tables;
        #endregion
    }
    #endregion
}
