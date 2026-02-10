using System.Collections.Generic;
using System.Data;
using AdoAsync;
using FluentAssertions;
using Xunit;

namespace AdoAsync.Tests;

public class CommandDefinitionFactoryTests
{
    [Fact]
    public void Create_SetsExplicitProperties()
    {
        var specs = new[]
        {
            new CommandDefinitionFactory.DbParameterSpec("@id", DbDataType.Int32, ParameterDirection.Input, 7)
        };
        var allowed = new HashSet<string> { "dbo.Items" };
        var identifiers = new List<string> { "dbo.Items" };

        var command = CommandDefinitionFactory.Create(
            "select * from dbo.Items",
            CommandType.Text,
            specs,
            commandTimeoutSeconds: 12,
            behavior: CommandBehavior.CloseConnection,
            allowedIdentifiers: allowed,
            identifiersToValidate: identifiers);

        command.CommandText.Should().Be("select * from dbo.Items");
        command.CommandType.Should().Be(CommandType.Text);
        command.Parameters.Should().NotBeNull();
        command.CommandTimeoutSeconds.Should().Be(12);
        command.Behavior.Should().Be(CommandBehavior.CloseConnection);
        command.AllowedIdentifiers.Should().BeSameAs(allowed);
        command.IdentifiersToValidate.Should().BeSameAs(identifiers);
    }

    [Fact]
    public void Create_WithEmptySpecs_UsesNullParameters()
    {
        var command = CommandDefinitionFactory.Create(
            "select 1",
            CommandType.Text,
            Array.Empty<CommandDefinitionFactory.DbParameterSpec>());

        command.Parameters.Should().BeNull();
    }

    [Fact]
    public void Create_WithNullSpecs_UsesNullParameters()
    {
        var command = CommandDefinitionFactory.Create(
            "select 1",
            CommandType.Text,
            parameterSpecs: null);

        command.Parameters.Should().BeNull();
    }

    [Fact]
    public void Create_SetsStoredProcedureCommandType()
    {
        var command = CommandDefinitionFactory.Create(
            "dbo.GetItem",
            CommandType.StoredProcedure);

        command.CommandType.Should().Be(CommandType.StoredProcedure);
        command.CommandText.Should().Be("dbo.GetItem");
    }

    [Fact]
    public void Create_FromSpecs_BuildsCommandWithParameters()
    {
        var specs = new List<CommandDefinitionFactory.DbParameterSpec>
        {
            new("@id", DbDataType.Int32, ParameterDirection.Input, 7),
            new("@message", DbDataType.String, ParameterDirection.Output, Size: 4000)
        };

        var command = CommandDefinitionFactory.Create(
            "dbo.GetItem",
            CommandType.StoredProcedure,
            specs,
            commandTimeoutSeconds: 15);

        command.CommandText.Should().Be("dbo.GetItem");
        command.CommandType.Should().Be(CommandType.StoredProcedure);
        command.CommandTimeoutSeconds.Should().Be(15);
        command.Parameters.Should().NotBeNull();
        command.Parameters!.Should().HaveCount(2);
    }

    [Fact]
    public void Create_WithDatabaseType_NormalizesParameterNames()
    {
        var specs = new List<CommandDefinitionFactory.DbParameterSpec>
        {
            new("id", DbDataType.Int32, ParameterDirection.Input, 7),
            new(":message", DbDataType.String, ParameterDirection.Output, Size: 4000)
        };

        var sqlServer = CommandDefinitionFactory.Create(
            "dbo.GetItem",
            CommandType.StoredProcedure,
            specs,
            databaseType: DatabaseType.SqlServer);

        var postgreSql = CommandDefinitionFactory.Create(
            "public.get_item",
            CommandType.StoredProcedure,
            specs,
            databaseType: DatabaseType.PostgreSql);

        sqlServer.Parameters!.Should().Contain(p => p.Name == "@id");
        sqlServer.Parameters!.Should().Contain(p => p.Name == "@message");
        postgreSql.Parameters!.Should().Contain(p => p.Name == "id");
        postgreSql.Parameters!.Should().Contain(p => p.Name == "message");
    }

    [Fact]
    public void Create_WithDatabaseType_SqlServer_DoesNotDoublePrefix()
    {
        var specs = new[]
        {
            new CommandDefinitionFactory.DbParameterSpec("@id", DbDataType.Int32, ParameterDirection.Input, 7),
            new CommandDefinitionFactory.DbParameterSpec("@message", DbDataType.String, ParameterDirection.Output, Size: 4000)
        };

        var sqlServer = CommandDefinitionFactory.Create(
            "dbo.GetItem",
            CommandType.StoredProcedure,
            specs,
            databaseType: DatabaseType.SqlServer);

        sqlServer.Parameters!.Should().Contain(p => p.Name == "@id");
        sqlServer.Parameters!.Should().Contain(p => p.Name == "@message");
        sqlServer.Parameters!.Should().NotContain(p => p.Name == "@@id");
        sqlServer.Parameters!.Should().NotContain(p => p.Name == "@@message");
    }
}
