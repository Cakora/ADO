using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using AdoAsync.Abstractions;

namespace AdoAsync.Providers.SqlServer;

/// <summary>
/// SQL Server provider implementation (parameter mapping and error translation).
/// </summary>
public sealed class SqlServerProvider : IDbProvider
{
    #region Public API
    /// <summary>Creates a SQL Server connection.</summary>
    public DbConnection CreateConnection(string connectionString)
    {
        Validate.Required(connectionString, nameof(connectionString));
        return new SqlConnection(connectionString);
    }

    /// <summary>Creates a SQL Server command.</summary>
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

    /// <summary>Applies parameters to a SQL Server command.</summary>
    public void ApplyParameters(DbCommand command, IEnumerable<DbParameter> parameters)
    {
        Validate.Required(command, nameof(command));
        Validate.Required(parameters, nameof(parameters));

        foreach (var param in parameters)
        {
            var sqlParam = new SqlParameter
            {
                ParameterName = param.Name,
                // ADO.NET providers expect DBNull for nulls.
                Value = param.Value ?? DBNull.Value,
                Direction = param.Direction
            };

            if (param.Size.HasValue)
            {
                // Explicit size avoids provider defaults that can truncate outputs.
                sqlParam.Size = param.Size.Value;
            }

            if (param.Precision.HasValue)
            {
                sqlParam.Precision = param.Precision.Value;
            }

            if (param.Scale.HasValue)
            {
                sqlParam.Scale = param.Scale.Value;
            }

            sqlParam.SqlDbType = SqlServerTypeMapper.Map(param.DataType);
            command.Parameters.Add(sqlParam);
        }
    }

    /// <summary>Performs SQL Server bulk import via SqlBulkCopy.</summary>
    public async ValueTask<int> BulkImportAsync(DbConnection connection, BulkImportRequest request, CancellationToken cancellationToken = default)
    {
        Validate.Required(connection, nameof(connection));
        Validate.Required(request, nameof(request));

        if (connection is not SqlConnection sqlConnection)
        {
            throw new DatabaseException(ErrorCategory.Configuration, "SQL Server bulk import requires a SqlConnection.");
        }

        using var bulkCopy = new SqlBulkCopy(sqlConnection)
        {
            DestinationTableName = request.DestinationTable,
            // Stream rows to keep memory usage stable on large imports.
            EnableStreaming = true,
            NotifyAfter = 1
        };

        if (request.BatchSize.HasValue)
        {
            bulkCopy.BatchSize = request.BatchSize.Value;
        }

        var rowsCopied = 0;
        bulkCopy.SqlRowsCopied += (_, args) => rowsCopied = (int)args.RowsCopied;

        foreach (var mapping in request.ColumnMappings)
        {
            bulkCopy.ColumnMappings.Add(mapping.SourceColumn, mapping.DestinationColumn);
        }

        await bulkCopy.WriteToServerAsync(request.SourceReader, cancellationToken).ConfigureAwait(false);
        return rowsCopied;
    }
    #endregion
}
