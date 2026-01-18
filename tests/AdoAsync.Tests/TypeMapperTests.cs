using System;
using AdoAsync;
using AdoAsync.Providers.Oracle;
using AdoAsync.Providers.PostgreSql;
using AdoAsync.Providers.SqlServer;
using FluentAssertions;
using Xunit;

namespace AdoAsync.Tests;

public class TypeMapperTests
{
    #region Tests
    [Fact]
    public void SqlServerTypeMapper_MapsCommonTypes()
    {
        SqlServerTypeMapper.Map(DbDataType.String).Should().Be(System.Data.SqlDbType.NVarChar);
        SqlServerTypeMapper.Map(DbDataType.Int32).Should().Be(System.Data.SqlDbType.Int);
    }

    [Fact]
    public void PostgreSqlTypeMapper_MapsCommonTypes()
    {
        PostgreSqlTypeMapper.Map(DbDataType.String).Should().Be(NpgsqlTypes.NpgsqlDbType.Text);
        PostgreSqlTypeMapper.Map(DbDataType.Int32).Should().Be(NpgsqlTypes.NpgsqlDbType.Integer);
    }

    [Fact]
    public void OracleTypeMapper_MapsCommonTypes()
    {
        OracleTypeMapper.Map(DbDataType.String).Should().Be(Oracle.ManagedDataAccess.Client.OracleDbType.NVarchar2);
        OracleTypeMapper.Map(DbDataType.Int32).Should().Be(Oracle.ManagedDataAccess.Client.OracleDbType.Int32);
    }

    [Fact]
    public void SqlServerTypeMapper_ThrowsOnUnsupportedType()
    {
        var act = () => SqlServerTypeMapper.Map((DbDataType)999);
        act.Should().Throw<DatabaseException>()
            .Where(e => e.Kind == ErrorCategory.Unsupported);
    }
    #endregion
}
