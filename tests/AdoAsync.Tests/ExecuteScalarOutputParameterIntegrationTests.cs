using System;
using System.Data;
using System.Threading.Tasks;
using AdoAsync;
using AdoAsync.Execution;
using FluentAssertions;
using Xunit;

namespace AdoAsync.Tests;

public class ExecuteScalarOutputParameterIntegrationTests
{
    // Integration tests are skipped by default to avoid requiring local DB setup.
    private const string SkipMessage = "Requires a live database with the expected schema/stored procedures.";

    [Fact(Skip = SkipMessage)]
    public async Task SqlServer_TextCommand_ReturnsScalarAndOutput()
    {
        var executor = CreateExecutor(DatabaseType.SqlServer);
        var command = CommandDefinitionFactory.Create(
            commandText: @"
            select count(*) from dbo.Customers where Id >= @minId;
            set @message = N'OK';
        ",
            commandType: CommandType.Text,
            parameterSpecs: new[]
            {
                new CommandDefinitionFactory.DbParameterSpec("@minId", DbDataType.Int32, ParameterDirection.Input, 100),
                new CommandDefinitionFactory.DbParameterSpec("@message", DbDataType.String, ParameterDirection.Output, Size: 4000)
            });

        var result = await executor.ExecuteScalarAsync<int>(command);

        result.OutputParameters.Should().ContainKey("message");
        result.OutputParameters["message"].Should().Be("OK");
        result.Value.Should().BeGreaterOrEqualTo(0);
    }

    [Fact(Skip = SkipMessage)]
    public async Task PostgreSql_StoredProcedure_ReturnsOutputs()
    {
        var executor = CreateExecutor(DatabaseType.PostgreSql);
        var command = CommandDefinitionFactory.Create(
            commandText: "public.get_customer_count_with_message",
            commandType: CommandType.StoredProcedure,
            parameterSpecs: new[]
            {
                new CommandDefinitionFactory.DbParameterSpec("p_min_id", DbDataType.Int32, ParameterDirection.Input, 100),
                new CommandDefinitionFactory.DbParameterSpec("p_total", DbDataType.Int32, ParameterDirection.Output),
                new CommandDefinitionFactory.DbParameterSpec("p_message", DbDataType.String, ParameterDirection.Output, Size: 4000)
            });

        var result = await executor.ExecuteScalarAsync<int>(command);

        result.OutputParameters.Should().ContainKey("p_total");
        result.OutputParameters.Should().ContainKey("p_message");
        result.OutputParameters["p_message"].Should().Be("OK");
        result.Value.Should().BeGreaterOrEqualTo(0);
    }

    [Fact(Skip = SkipMessage)]
    public async Task Oracle_StoredProcedure_ReturnsOutputs()
    {
        var executor = CreateExecutor(DatabaseType.Oracle);
        var command = CommandDefinitionFactory.Create(
            commandText: "get_customer_count_with_message",
            commandType: CommandType.StoredProcedure,
            parameterSpecs: new[]
            {
                new CommandDefinitionFactory.DbParameterSpec("p_min_id", DbDataType.Int32, ParameterDirection.Input, 100),
                new CommandDefinitionFactory.DbParameterSpec("p_total", DbDataType.Int32, ParameterDirection.Output),
                new CommandDefinitionFactory.DbParameterSpec("p_message", DbDataType.String, ParameterDirection.Output, Size: 4000)
            });

        var result = await executor.ExecuteScalarAsync<int>(command);

        result.OutputParameters.Should().ContainKey("p_total");
        result.OutputParameters.Should().ContainKey("p_message");
        result.OutputParameters["p_message"].Should().Be("OK");
        result.Value.Should().BeGreaterOrEqualTo(0);
    }

    private static DbExecutor CreateExecutor(DatabaseType databaseType)
        => DbExecutor.Create(new DbOptions
        {
            DatabaseType = databaseType,
            ConnectionString = Environment.GetEnvironmentVariable("ADOASYNC_TEST_CONNECTION") ?? string.Empty,
            CommandTimeoutSeconds = 30,
            EnableValidation = true
        });
}
