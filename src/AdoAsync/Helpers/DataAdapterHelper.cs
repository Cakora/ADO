using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using AdoAsync.Extensions.Execution;
using Microsoft.Data.SqlClient;
using Npgsql;
using Oracle.ManagedDataAccess.Client;

namespace AdoAsync.Helpers;

internal static class DataAdapterHelper
{
    /// <summary>Fill a DataTable using the provider DataAdapter (synchronous fill; cancellation checked beforehand).</summary>
    /// <param name="command">Prepared command ready for execution.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Filled DataTable.</returns>
    public static ValueTask<DataTable> FillTableAsync(DbCommand command, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // DataAdapter.Fill is synchronous by design; cancellation is checked before invocation.
        using var adapter = CreateAdapter(command);
        var table = new DataTable();
        adapter.Fill(table);
        return new ValueTask<DataTable>(table);
    }

    /// <summary>Fill all result sets into tables using the provider DataAdapter (synchronous fill; cancellation checked beforehand).</summary>
    /// <param name="command">Prepared command ready for execution.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>All result sets as DataTables.</returns>
    public static ValueTask<List<DataTable>> FillTablesAsync(DbCommand command, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // DataAdapter.Fill is synchronous by design; cancellation is checked before invocation.
        using var adapter = CreateAdapter(command);
        var dataSet = new DataSet();
        adapter.Fill(dataSet);

        var tables = new List<DataTable>(dataSet.Tables.Count);
        foreach (DataTable table in dataSet.Tables)
        {
            tables.Add(table);
        }

        return new ValueTask<List<DataTable>>(tables);
    }

    /// <summary>Fill a DataSet using the provider DataAdapter (synchronous fill; cancellation checked beforehand).</summary>
    /// <param name="command">Prepared command ready for execution.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Filled DataSet.</returns>
    public static ValueTask<DataSet> FillDataSetAsync(DbCommand command, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // DataAdapter.Fill is synchronous by design; cancellation is checked before invocation.
        using var adapter = CreateAdapter(command);
        var dataSet = new DataSet();
        adapter.Fill(dataSet);
        return new ValueTask<DataSet>(dataSet);
    }

    /// <summary>Create a provider-specific DataAdapter for the given command.</summary>
    /// <param name="command">Command whose provider type determines the adapter.</param>
    /// <returns>Provider-specific DataAdapter.</returns>
    /// <exception cref="DatabaseException">Thrown when the provider is unsupported.</exception>
    private static DbDataAdapter CreateAdapter(DbCommand command) =>
        command switch
        {
            SqlCommand sqlCommand => new SqlDataAdapter(sqlCommand),
            NpgsqlCommand npgsqlCommand => new NpgsqlDataAdapter(npgsqlCommand),
            OracleCommand oracleCommand => new OracleDataAdapter(oracleCommand),
            _ => throw new DatabaseException(ErrorCategory.Unsupported, $"DataAdapter is not supported for command type '{command.GetType().Name}'.")
        };
}
