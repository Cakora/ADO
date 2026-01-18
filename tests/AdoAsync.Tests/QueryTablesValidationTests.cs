using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using AdoAsync;
using AdoAsync.Execution;
using FluentAssertions;
using Xunit;

namespace AdoAsync.Tests;

public class QueryTablesValidationTests
{
    [Theory]
    [InlineData(DatabaseType.SqlServer)]
    [InlineData(DatabaseType.PostgreSql)]
    [InlineData(DatabaseType.Oracle)]
    public async Task QueryTablesAsync_InvalidCommandText_ReturnsValidationError(DatabaseType databaseType)
    {
        var executor = DbExecutor.Create(new DbOptions
        {
            DatabaseType = databaseType,
            ConnectionString = "Server=localhost;Database=Dummy;User Id=Dummy;Password=Dummy;",
            CommandTimeoutSeconds = 30,
            EnableValidation = true
        });

        var result = await executor.QueryTablesAsync(new CommandDefinition
        {
            CommandText = string.Empty,
            CommandType = CommandType.Text
        });

        result.Success.Should().BeFalse();
        result.Error.Should().NotBeNull();
        result.Error!.Type.Should().Be(DbErrorType.ValidationError);
    }

    [Theory]
    [InlineData(DatabaseType.SqlServer)]
    [InlineData(DatabaseType.PostgreSql)]
    [InlineData(DatabaseType.Oracle)]
    public async Task QueryTablesAsync_StoredProcedureMissingParameters_ReturnsValidationError(DatabaseType databaseType)
    {
        var executor = DbExecutor.Create(new DbOptions
        {
            DatabaseType = databaseType,
            ConnectionString = "Server=localhost;Database=Dummy;User Id=Dummy;Password=Dummy;",
            CommandTimeoutSeconds = 30,
            EnableValidation = true
        });

        // Stored procedures require a parameter collection (even if empty).
        var result = await executor.QueryTablesAsync(new CommandDefinition
        {
            CommandText = "dbo.proc_name",
            CommandType = CommandType.StoredProcedure
        });

        result.Success.Should().BeFalse();
        result.Error.Should().NotBeNull();
        result.Error!.Type.Should().Be(DbErrorType.ValidationError);
    }
}
