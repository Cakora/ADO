using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Threading;
using System.Buffers;
using System.Text;
using System.Threading.Tasks;
using Npgsql;
using AdoAsync;
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
    public async ValueTask<int> BulkImportAsync(DbConnection connection, BulkImportRequest request, CancellationToken cancellationToken = default)
    {
        Validate.Required(connection, nameof(connection));
        Validate.Required(request, nameof(request));

        if (connection is not NpgsqlConnection npgConnection)
        {
            throw new DatabaseException(ErrorCategory.Configuration, "PostgreSQL bulk import requires an NpgsqlConnection.");
        }

        var copyCommand = BuildCopyCommand(request);
        // Binary import is the fastest COPY path for large datasets.
        await using var importer = npgConnection.BeginBinaryImport(copyCommand);

        var rows = 0;
        var ordinalCount = request.ColumnMappings.Count;
        var ordinals = ArrayPool<int>.Shared.Rent(ordinalCount);
        try
        {
            // Pool ordinal buffers to reduce per-call allocations during bulk imports.
            for (var i = 0; i < ordinalCount; i++)
            {
                // Resolve ordinals once to avoid per-row name lookups.
                ordinals[i] = request.SourceReader.GetOrdinal(request.ColumnMappings[i].SourceColumn);
            }

            while (await request.SourceReader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                cancellationToken.ThrowIfCancellationRequested();
                await importer.StartRowAsync(cancellationToken).ConfigureAwait(false);

                for (var i = 0; i < ordinalCount; i++)
                {
                    var value = request.SourceReader.GetValue(ordinals[i]);
                    if (value is null || value is DBNull)
                    {
                        // Explicit null handling keeps COPY format stable across rows.
                        await importer.WriteNullAsync(cancellationToken).ConfigureAwait(false);
                    }
                    else
                    {
                        await importer.WriteAsync(value, cancellationToken).ConfigureAwait(false);
                    }
                }

                rows++;
            }
        }
        finally
        {
            Array.Clear(ordinals, 0, ordinalCount);
            ArrayPool<int>.Shared.Return(ordinals);
        }

        await importer.CompleteAsync(cancellationToken).ConfigureAwait(false);
        return rows;
    }
    #endregion

    #region Private Helpers
    private static string BuildCopyCommand(BulkImportRequest request)
    {
        // ReadOnlyMemory keeps identifier parsing allocation-light.
        var tableName = QuoteIdentifier(request.DestinationTable.AsMemory());
        var columns = new string[request.ColumnMappings.Count];
        for (var i = 0; i < request.ColumnMappings.Count; i++)
        {
            columns[i] = QuoteIdentifier(request.ColumnMappings[i].DestinationColumn.AsMemory());
        }

        // COPY requires quoted identifiers to preserve casing and reserved words.
        var columnList = string.Join(", ", columns);
        return $"COPY {tableName} ({columnList}) FROM STDIN (FORMAT BINARY)";
    }

    private static string QuoteIdentifier(ReadOnlyMemory<char> identifier)
    {
        var span = identifier.Span;
        var builder = new StringBuilder(span.Length + 2);
        var segmentStart = 0;
        var firstSegment = true;

        for (var i = 0; i <= span.Length; i++)
        {
            if (i < span.Length && span[i] != '.')
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
                var ch = span[j];
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
    #endregion
}
