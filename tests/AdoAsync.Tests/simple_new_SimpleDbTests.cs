using System.Collections.Generic;
using System.Data;
using AdoAsync.Simple;
using FluentAssertions;
using Xunit;

namespace AdoAsync.Tests;

public class simple_new_SimpleDbTests
{
    private const string SkipMessage = "Requires live database connections for provider-specific tests.";

    [Fact]
    public void SimpleParameter_ValueCtor_DefaultsToInput()
    {
        var parameter = new SimpleParameter("id", 7);

        parameter.Name.Should().Be("id");
        parameter.Value.Should().Be(7);
        parameter.Direction.Should().Be(ParameterDirection.Input);
        parameter.DataType.Should().BeNull();
        parameter.Size.Should().BeNull();
    }

    [Fact]
    public void SimpleParameter_OutputCtor_SetsTypeDirectionAndSize()
    {
        var parameter = new SimpleParameter("message", DbDataType.String, ParameterDirection.Output, 4000);

        parameter.Name.Should().Be("message");
        parameter.Value.Should().BeNull();
        parameter.DataType.Should().Be(DbDataType.String);
        parameter.Direction.Should().Be(ParameterDirection.Output);
        parameter.Size.Should().Be(4000);
    }

    [Fact(Skip = SkipMessage)]
    public async Task SqlServer_QueryTableAsync_Works()
    {
        using var db = new SqlServerSimpleDb();
        var parameters = new List<SimpleParameter> { new("minId", 100) };
        var common = new CommonProcessInput { ConnectionString = "Server=.;Database=MyDb;Trusted_Connection=True;" };
        _ = await db.QueryTableAsync("dbo.GetCustomers", common, CommandType.StoredProcedure, parameters);
    }

    [Fact(Skip = SkipMessage)]
    public async Task SqlServer_QueryTableAsync_WithOptions_Works()
    {
        using var db = new SqlServerSimpleDb();
        var parameters = new List<SimpleParameter> { new("minId", 100) };
        var common = new CommonProcessInput
        {
            ConnectionString = "Server=.;Database=MyDb;Trusted_Connection=True;",
            CommandTimeoutSeconds = 30
        };
        _ = await db.QueryTableAsync("dbo.GetCustomers", common, CommandType.StoredProcedure, parameters);
    }

    [Fact(Skip = SkipMessage)]
    public async Task SqlServer_ExecuteNonQueryAsync_WithoutParameters_Works()
    {
        using var db = new SqlServerSimpleDb();
        var common = new CommonProcessInput { ConnectionString = "Server=.;Database=MyDb;Trusted_Connection=True;" };
        _ = await db.ExecuteNonQueryAsync("SELECT 1", common, CommandType.Text);
    }

    [Fact(Skip = SkipMessage)]
    public async Task SqlServer_BeginTransaction_DisposesConnection()
    {
        using var db = new SqlServerSimpleDb();
        var common = new CommonProcessInput { ConnectionString = "Server=.;Database=MyDb;Trusted_Connection=True;" };
        var (connection, transaction) = await db.BeginTransactionAsync(common);

        await transaction.DisposeAsync();
        await connection.DisposeAsync();

        connection.State.Should().Be(ConnectionState.Closed);
    }

    [Fact(Skip = SkipMessage)]
    public async Task SqlServer_SharedTransaction_Rollback_DoesNotCommit()
    {
        using var db = new SqlServerSimpleDb();
        var common = new CommonProcessInput { ConnectionString = "Server=.;Database=MyDb;Trusted_Connection=True;" };
        var (connection, transaction) = await db.BeginTransactionAsync(common);

        try
        {
            await db.ExecuteNonQueryAsync(
                commandText: "CREATE TABLE #temp_test(id int);",
                common: common,
                transaction: transaction,
                commandType: CommandType.Text);

            await transaction.RollbackAsync();
        }
        finally
        {
            await transaction.DisposeAsync();
            await connection.DisposeAsync();
        }
    }

    [Fact(Skip = SkipMessage)]
    public async Task PostgreSql_QueryTableAsync_Works()
    {
        using var db = new PostgreSqlSimpleDb();
        var parameters = new List<SimpleParameter> { new("min_id", 100) };
        var common = new CommonProcessInput { ConnectionString = "Host=localhost;Database=mydb;Username=myuser;Password=mypassword" };
        _ = await db.QueryTableAsync("public.get_customers", common, CommandType.StoredProcedure, parameters);
    }

    [Fact(Skip = SkipMessage)]
    public async Task PostgreSql_QueryTableAsync_WithOptions_Works()
    {
        using var db = new PostgreSqlSimpleDb();
        var parameters = new List<SimpleParameter> { new("min_id", 100) };
        var common = new CommonProcessInput
        {
            ConnectionString = "Host=localhost;Database=mydb;Username=myuser;Password=mypassword",
            CommandTimeoutSeconds = 30
        };
        _ = await db.QueryTableAsync("public.get_customers", common, CommandType.StoredProcedure, parameters);
    }

    [Fact(Skip = SkipMessage)]
    public async Task Oracle_QueryTableAsync_Works()
    {
        using var db = new OracleSimpleDb();
        var parameters = new List<SimpleParameter> { new("p_min_id", 100) };
        var common = new CommonProcessInput { ConnectionString = "User Id=myuser;Password=mypassword;Data Source=MyOracleDb" };
        _ = await db.QueryTableAsync("PKG_CUSTOMER.GET_CUSTOMERS", common, CommandType.StoredProcedure, parameters);
    }

    [Fact(Skip = SkipMessage)]
    public async Task Oracle_QueryTableAsync_WithOptions_Works()
    {
        using var db = new OracleSimpleDb();
        var parameters = new List<SimpleParameter> { new("p_min_id", 100) };
        var common = new CommonProcessInput
        {
            ConnectionString = "User Id=myuser;Password=mypassword;Data Source=MyOracleDb",
            CommandTimeoutSeconds = 30
        };
        _ = await db.QueryTableAsync("PKG_CUSTOMER.GET_CUSTOMERS", common, CommandType.StoredProcedure, parameters);
    }
}
