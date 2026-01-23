using System;
using System.Buffers;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Npgsql;
using NpgsqlTypes;
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
        Validate.Required(connectionString, nameof(connectionString));
        return new NpgsqlConnection(connectionString);
    }

    /// <summary>Creates a PostgreSQL command.</summary>
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

    /// <summary>Applies parameters to a PostgreSQL command.</summary>
    public void ApplyParameters(DbCommand command, IEnumerable<DbParameter> parameters)
    {
        Validate.Required(command, nameof(command));
        Validate.Required(parameters, nameof(parameters));

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
    public async ValueTask<int> BulkImportAsync(DbConnection connection, DbTransaction? transaction, BulkImportRequest request, CancellationToken cancellationToken = default)
    {
        Validate.Required(connection, nameof(connection));
        Validate.Required(request, nameof(request));

        if (connection is not NpgsqlConnection npgConnection)
        {
            throw new DatabaseException(ErrorCategory.Configuration, "PostgreSQL bulk import requires an NpgsqlConnection.");
        }

        if (transaction is not null && transaction is not NpgsqlTransaction)
        {
            throw new DatabaseException(ErrorCategory.Configuration, "PostgreSQL bulk import requires an NpgsqlTransaction when a transaction is provided.");
        }

        var copyCommand = BuildCopyCommand(request);
        await using var importer = await npgConnection.BeginBinaryImportAsync(copyCommand, cancellationToken).ConfigureAwait(false);

        var columnCount = request.ColumnMappings.Count;
        // ArrayPool avoids per-import allocations when reading ordinals.
        var ordinals = ArrayPool<int>.Shared.Rent(columnCount);
        try
        {
            for (var i = 0; i < columnCount; i++)
            {
                ordinals[i] = request.SourceReader.GetOrdinal(request.ColumnMappings[i].SourceColumn);
            }

            var rows = 0;
            while (await request.SourceReader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                cancellationToken.ThrowIfCancellationRequested();
                await importer.StartRowAsync(cancellationToken).ConfigureAwait(false);

                for (var i = 0; i < columnCount; i++)
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
        finally
        {
            Array.Clear(ordinals, 0, columnCount);
            ArrayPool<int>.Shared.Return(ordinals);
        }
    }
    #endregion

    #region Private Helpers
    private static string BuildCopyCommand(BulkImportRequest request)
    {
        var tableName = QuoteIdentifier(request.DestinationTable.AsSpan());
        var columns = new string[request.ColumnMappings.Count];
        for (var i = 0; i < request.ColumnMappings.Count; i++)
        {
            columns[i] = QuoteIdentifier(request.ColumnMappings[i].DestinationColumn.AsSpan());
        }

        var columnList = string.Join(", ", columns);
        return $"COPY {tableName} ({columnList}) FROM STDIN (FORMAT BINARY)";
    }

    private static string QuoteIdentifier(ReadOnlySpan<char> identifier)
    {
        // Quote/escape to preserve case and prevent injection via identifiers.
        var builder = new StringBuilder(identifier.Length + 2);
        var segmentStart = 0;
        var firstSegment = true;

        for (var i = 0; i <= identifier.Length; i++)
        {
            if (i < identifier.Length && identifier[i] != '.')
            {
                continue;
            }

            if (!firstSegment)
            {
                builder.Append('.');
            }

            firstSegment = false;
            builder.Append('"');

            for (var j = segmentStart; j < i; j++)
            {
                var ch = identifier[j];
                if (ch == '"')
                {
                    builder.Append("\"\"");
                }
                else
                {
                    builder.Append(ch);
                }
            }

            builder.Append('"');
            segmentStart = i + 1;
        }

        return builder.ToString();
    }

    internal static async Task<IReadOnlyList<DataTable>> ReadRefCursorResultsAsync(
        DbCommand command,
        DbConnection connection,
        DbTransaction transaction,
        CancellationToken cancellationToken)
    {
        #region Refcursor
        var tables = new List<DataTable>();
        foreach (System.Data.Common.DbParameter parameter in command.Parameters)
        {
            if (parameter is not NpgsqlParameter npgParam || npgParam.NpgsqlDbType != NpgsqlDbType.Refcursor)
            {
                continue;
            }

            if (npgParam.Value is not string cursorName || string.IsNullOrWhiteSpace(cursorName))
            {
                continue;
            }

            await using var fetch = connection.CreateCommand();
            fetch.Transaction = transaction;
            // PostgreSQL refcursors must be fetched explicitly within the same transaction.
            fetch.CommandText = $"FETCH ALL IN {QuoteCursorName(cursorName.AsSpan())}";
            await using var reader = await fetch.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            var table = new DataTable();
            table.Load(reader);
            tables.Add(table);
        }

        return tables;
        #endregion
    }

    private static string QuoteCursorName(ReadOnlySpan<char> name)
    {
        var builder = new StringBuilder(name.Length + 2);
        builder.Append('"');
        for (var i = 0; i < name.Length; i++)
        {
            var ch = name[i];
            if (ch == '"')
            {
                builder.Append("\"\"");
            }
            else
            {
                builder.Append(ch);
            }
        }
        builder.Append('"');
        return builder.ToString();
    }
    #endregion
}
