using System.Collections.Generic;
using System.Data;
using AdoAsync.Helpers;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Xunit;

namespace AdoAsync.Tests;

public class ParameterHelperTests
{
    [Fact]
    public void ExtractOutputParameters_NormalizesOutputs()
    {
        using var command = new SqlCommand();
        command.Parameters.Add(new SqlParameter("@id", SqlDbType.Decimal)
        {
            Direction = ParameterDirection.Output,
            Value = 42m
        });
        command.Parameters.Add(new SqlParameter("@in", SqlDbType.Int)
        {
            Direction = ParameterDirection.Input,
            Value = 7
        });

        var definitions = new List<DbParameter>
        {
            new() { Name = "@id", DataType = DbDataType.Int32, Direction = ParameterDirection.Output, Size = 4 },
            new() { Name = "@in", DataType = DbDataType.Int32, Direction = ParameterDirection.Input }
        };

        var outputs = ParameterHelper.ExtractOutputParameters(command, definitions);

        outputs.Should().NotBeNull();
        outputs!.Should().ContainKey("id");
        outputs!["id"].Should().Be(42);
    }

    [Fact]
    public void ExtractOutputParameters_SkipsRefCursorOutputs()
    {
        using var command = new SqlCommand();
        command.Parameters.Add(new SqlParameter("@cursor", SqlDbType.VarChar)
        {
            Direction = ParameterDirection.Output,
            Value = "ignored"
        });

        var definitions = new List<DbParameter>
        {
            new() { Name = "@cursor", DataType = DbDataType.RefCursor, Direction = ParameterDirection.Output }
        };

        var outputs = ParameterHelper.ExtractOutputParameters(command, definitions);

        outputs.Should().BeNull();
    }

    [Fact]
    public void ExtractOutputParameters_ConvertsDbNullToNull()
    {
        using var command = new SqlCommand();
        command.Parameters.Add(new SqlParameter("@value", SqlDbType.NVarChar, 50)
        {
            Direction = ParameterDirection.Output,
            Value = System.DBNull.Value
        });

        var definitions = new List<DbParameter>
        {
            new() { Name = "@value", DataType = DbDataType.String, Direction = ParameterDirection.Output, Size = 50 }
        };

        var outputs = ParameterHelper.ExtractOutputParameters(command, definitions);

        outputs.Should().NotBeNull();
        outputs!["value"].Should().BeNull();
    }
}
