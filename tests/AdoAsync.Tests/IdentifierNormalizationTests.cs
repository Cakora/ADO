using AdoAsync;
using FluentAssertions;
using Xunit;

namespace AdoAsync.Tests;

public class IdentifierNormalizationTests
{
    [Fact]
    public void NormalizeTableName_Oracle_UppercasesUnquotedSegments()
    {
        var normalized = IdentifierNormalization.NormalizeTableName(DatabaseType.Oracle, "app.orders");
        normalized.Should().Be("APP.ORDERS");
    }

    [Fact]
    public void NormalizeTableName_Oracle_PreservesQuotedIdentifiers()
    {
        var normalized = IdentifierNormalization.NormalizeTableName(DatabaseType.Oracle, "\"App\".\"Orders\"");
        normalized.Should().Be("\"App\".\"Orders\"");
    }

    [Fact]
    public void NormalizeTableName_NonOracle_DoesNotChange()
    {
        var normalized = IdentifierNormalization.NormalizeTableName(DatabaseType.SqlServer, "App.Orders");
        normalized.Should().Be("App.Orders");
    }
}

