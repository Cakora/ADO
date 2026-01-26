using System.Data;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using AdoAsync.Providers.Oracle;
using AdoAsync.Providers.SqlServer;
using Oracle.ManagedDataAccess.Client;
using Xunit;

namespace AdoAsync.Tests;

public sealed class ProviderParameterBindingTests
{
    [Fact]
    public void SqlServerProvider_ApplyParameters_StructuredTvp_SetsTypeNameAndStructuredDbType()
    {
        var provider = new SqlServerProvider();
        using var command = new SqlCommand();
        var tvp = new DataTable();
        tvp.Columns.Add("Id", typeof(int));
        tvp.Rows.Add(1);

        provider.ApplyParameters(command, new[]
        {
            tvp.ToTvp(parameterName: "@Rows", structuredTypeName: "dbo.MyRowType")
        });

        command.Parameters.Count.Should().Be(1);
        var parameter = command.Parameters[0]!;
        parameter.SqlDbType.Should().Be(SqlDbType.Structured);
        parameter.TypeName.Should().Be("dbo.MyRowType");
        parameter.Value.Should().BeSameAs(tvp);
    }

    [Fact]
    public void SqlServerProvider_ApplyParameters_StructuredTvp_MissingTypeName_Throws()
    {
        var provider = new SqlServerProvider();
        using var command = new SqlCommand();
        var tvp = new DataTable();

        var act = () => provider.ApplyParameters(command, new[]
        {
            new DbParameter
            {
                Name = "@Rows",
                DataType = DbDataType.Structured,
                Direction = ParameterDirection.Input,
                Value = tvp
            }
        });

        act.Should().Throw<DatabaseException>();
    }

    [Fact]
    public void OracleProvider_ApplyParameters_ArrayBinding_SetsArrayBindCountAndCollectionType()
    {
        var provider = new OracleProvider();
        using var command = new OracleCommand();

        var rows = new[]
        {
            new OracleRow(1, "READY"),
            new OracleRow(2, "FAILED"),
            new OracleRow(3, "DONE")
        };

        var parameters = new[]
        {
            rows.ToArrayBindingParameter(":p_destination_id", DbDataType.Int32, r => r.DestinationId),
            rows.ToArrayBindingParameter(":p_state", DbDataType.String, r => r.State, size: 50)
        };

        provider.ApplyParameters(command, parameters);

        command.ArrayBindCount.Should().Be(3);
        command.Parameters.Count.Should().Be(2);
        command.Parameters[0]!.CollectionType.Should().Be(OracleCollectionType.PLSQLAssociativeArray);
        command.Parameters[1]!.CollectionType.Should().Be(OracleCollectionType.PLSQLAssociativeArray);
        command.Parameters[0]!.Size.Should().Be(3);
        command.Parameters[1]!.Size.Should().Be(3);
        command.Parameters[1]!.ArrayBindSize.Should().NotBeNull();
        command.Parameters[1]!.ArrayBindSize.Should().HaveCount(3);
        command.Parameters[1]!.ArrayBindSize.Should().OnlyContain(x => x == 50);
    }

    [Fact]
    public void OracleProvider_ApplyParameters_ArrayBinding_LengthMismatch_Throws()
    {
        var provider = new OracleProvider();
        using var command = new OracleCommand();

        var act = () => provider.ApplyParameters(command, new[]
        {
            new DbParameter
            {
                Name = ":p_destination_id",
                DataType = DbDataType.Int32,
                Direction = ParameterDirection.Input,
                IsArrayBinding = true,
                Value = new[] { 1, 2, 3 }
            },
            new DbParameter
            {
                Name = ":p_state",
                DataType = DbDataType.String,
                Direction = ParameterDirection.Input,
                IsArrayBinding = true,
                Size = 50,
                Value = new[] { "READY" }
            }
        });

        act.Should().Throw<DatabaseException>();
    }

    [Fact]
    public void DbParameterBindingExtensions_ToOracleArrayBindingParameters_EmptyRows_Throws()
    {
        var rows = Array.Empty<int>();
        var act = () => rows.ToArrayBindingParameter(":p_id", DbDataType.Int32, r => r);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void DbParameterBindingExtensions_ToOracleArrayBindingParameters_StringMissingSize_Throws()
    {
        var rows = new[] { "A", "B" };
        var act = () => rows.ToArrayBindingParameter(":p_state", DbDataType.String, r => r);

        act.Should().Throw<ArgumentException>();
    }

    private sealed record OracleRow(int DestinationId, string State);
}
