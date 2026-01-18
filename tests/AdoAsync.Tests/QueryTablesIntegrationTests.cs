using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using AdoAsync;
using AdoAsync.Execution;
using Xunit;

namespace AdoAsync.Tests;

public class QueryTablesIntegrationTests
{
    // Integration tests are skipped by default to avoid requiring local DB setup.
    private const string SkipMessage = "Requires a live database with the expected schema/stored procedures.";

    [Theory(Skip = SkipMessage)]
    [InlineData(DatabaseType.SqlServer, "select 1 as Id")]
    [InlineData(DatabaseType.PostgreSql, "select 1 as Id")]
    [InlineData(DatabaseType.Oracle, "select 1 as Id from dual")]
    public async Task SingleSelect_ReturnsTable(DatabaseType databaseType, string sql)
    {
        var result = await ExecuteQueryTablesAsync(databaseType, sql, CommandType.Text);
        Assert.True(result.Success);
        Assert.NotNull(result.Tables);
        Assert.NotEmpty(result.Tables);
    }

    [Theory(Skip = SkipMessage)]
    [InlineData(DatabaseType.SqlServer, "select 1 as Id, 'Name' as Name")]
    [InlineData(DatabaseType.PostgreSql, "select 1 as Id, 'Name' as Name")]
    [InlineData(DatabaseType.Oracle, "select 1 as Id, 'Name' as Name from dual")]
    public async Task SingleSelect_CustomClassMapping(DatabaseType databaseType, string sql)
    {
        var executor = CreateExecutor(databaseType);
        var command = new CommandDefinition
        {
            CommandText = sql,
            CommandType = CommandType.Text
        };

        var results = new List<SampleRow>();
        await foreach (var row in executor.QueryAsync(command, record => new SampleRow
        {
            Id = record.GetInt32(0),
            Name = record.GetString(1)
        }))
        {
            results.Add(row);
        }

        Assert.NotEmpty(results);
    }

    [Theory(Skip = SkipMessage)]
    [InlineData(DatabaseType.SqlServer, "dbo.GetSingleResult")]
    [InlineData(DatabaseType.PostgreSql, "public.get_single_result")]
    [InlineData(DatabaseType.Oracle, "PKG_TEST.GET_SINGLE_RESULT")]
    public async Task StoredProcedure_ReturnsTable(DatabaseType databaseType, string procedureName)
    {
        var result = await ExecuteQueryTablesAsync(databaseType, procedureName, CommandType.StoredProcedure);
        Assert.True(result.Success);
        Assert.NotNull(result.Tables);
        Assert.NotEmpty(result.Tables);
    }

    [Theory(Skip = SkipMessage)]
    [InlineData(DatabaseType.SqlServer, "dbo.GetSingleResult")]
    [InlineData(DatabaseType.PostgreSql, "public.get_single_result")]
    [InlineData(DatabaseType.Oracle, "PKG_TEST.GET_SINGLE_RESULT")]
    public async Task StoredProcedure_CustomClassMapping(DatabaseType databaseType, string procedureName)
    {
        var executor = CreateExecutor(databaseType);
        var command = new CommandDefinition
        {
            CommandText = procedureName,
            CommandType = CommandType.StoredProcedure,
            Parameters = new List<DbParameter>()
        };

        var results = new List<SampleRow>();
        await foreach (var row in executor.QueryAsync(command, record => new SampleRow
        {
            Id = record.GetInt32(0),
            Name = record.GetString(1)
        }))
        {
            results.Add(row);
        }

        Assert.NotEmpty(results);
    }

    [Theory(Skip = SkipMessage)]
    [InlineData(DatabaseType.SqlServer, "dbo.GetCustomerStats")]
    [InlineData(DatabaseType.PostgreSql, "public.get_customer_stats")]
    [InlineData(DatabaseType.Oracle, "PKG_CUSTOMER.GET_CUSTOMER_STATS")]
    public async Task StoredProcedure_OutputParameters_Returned(DatabaseType databaseType, string procedureName)
    {
        var result = await ExecuteQueryTablesAsync(databaseType, procedureName, CommandType.StoredProcedure, new[]
        {
            new DbParameter
            {
                Name = databaseType == DatabaseType.Oracle ? ":p_customer_id" : "@p_customer_id",
                DataType = DbDataType.Int32,
                Direction = ParameterDirection.Input,
                Value = 42
            },
            new DbParameter
            {
                Name = databaseType == DatabaseType.Oracle ? ":p_order_count" : "@p_order_count",
                DataType = DbDataType.Int32,
                Direction = ParameterDirection.Output,
                Size = 4
            }
        });

        Assert.True(result.Success);
        Assert.NotNull(result.OutputParameters);
    }

    [Theory(Skip = SkipMessage)]
    [InlineData(DatabaseType.SqlServer, "dbo.GetMultiResult")]
    [InlineData(DatabaseType.PostgreSql, "public.get_multi_result")]
    [InlineData(DatabaseType.Oracle, "PKG_TEST.GET_MULTI_RESULT")]
    public async Task StoredProcedure_MultiResult_ReturnsTables(DatabaseType databaseType, string procedureName)
    {
        var result = await ExecuteQueryTablesAsync(databaseType, procedureName, CommandType.StoredProcedure);
        Assert.True(result.Success);
        Assert.NotNull(result.Tables);
        Assert.True(result.Tables!.Count >= 2);
    }

    [Theory(Skip = SkipMessage)]
    [InlineData(DatabaseType.SqlServer, "dbo.GetMultiResult")]
    [InlineData(DatabaseType.PostgreSql, "public.get_multi_result")]
    [InlineData(DatabaseType.Oracle, "PKG_TEST.GET_MULTI_RESULT")]
    public async Task StoredProcedure_MultiResult_DataSet(DatabaseType databaseType, string procedureName)
    {
        var result = await ExecuteQueryTablesAsync(databaseType, procedureName, CommandType.StoredProcedure);
        Assert.True(result.Success);
        Assert.NotNull(result.Tables);

        var dataSet = new DataSet();
        foreach (var table in result.Tables!)
        {
            dataSet.Tables.Add(table);
        }

        Assert.True(dataSet.Tables.Count >= 2);
    }

    private static DbExecutor CreateExecutor(DatabaseType databaseType)
        => DbExecutor.Create(new DbOptions
        {
            DatabaseType = databaseType,
            ConnectionString = Environment.GetEnvironmentVariable("ADOASYNC_TEST_CONNECTION") ?? string.Empty,
            CommandTimeoutSeconds = 30,
            EnableValidation = true
        });

    private static ValueTask<DbResult> ExecuteQueryTablesAsync(
        DatabaseType databaseType,
        string commandText,
        CommandType commandType,
        IReadOnlyList<DbParameter>? parameters = null)
    {
        var executor = CreateExecutor(databaseType);

        return executor.QueryTablesAsync(new CommandDefinition
        {
            CommandText = commandText,
            CommandType = commandType,
            Parameters = parameters ?? new List<DbParameter>()
        });
    }

    private sealed class SampleRow
    {
        public int Id { get; init; }
        public string Name { get; init; } = string.Empty;
    }
}
