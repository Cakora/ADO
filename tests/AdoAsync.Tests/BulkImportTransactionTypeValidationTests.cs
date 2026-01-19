using System;
using System.Data;
using System.Data.Common;
using System.Threading.Tasks;
using AdoAsync.Providers.Oracle;
using AdoAsync.Providers.PostgreSql;
using AdoAsync.Providers.SqlServer;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Npgsql;
using Oracle.ManagedDataAccess.Client;
using Xunit;

namespace AdoAsync.Tests;

public class BulkImportTransactionTypeValidationTests
{
    [Fact]
    public async Task SqlServerProvider_BulkImportAsync_RejectsNonSqlTransaction()
    {
        var provider = new SqlServerProvider();
        using var connection = new SqlConnection("Server=localhost;Database=master;Trusted_Connection=True;Encrypt=False;");

        var request = CreateRequest();
        var ex = await Assert.ThrowsAsync<DatabaseException>(() =>
            provider.BulkImportAsync(connection, new FakeDbTransaction(), request).AsTask());

        ex.Kind.Should().Be(ErrorCategory.Configuration);
    }

    [Fact]
    public async Task OracleProvider_BulkImportAsync_RejectsNonOracleTransaction()
    {
        var provider = new OracleProvider();
        using var connection = new OracleConnection("Data Source=localhost/XEPDB1;User Id=user;Password=pw;");

        var request = CreateRequest();
        var ex = await Assert.ThrowsAsync<DatabaseException>(() =>
            provider.BulkImportAsync(connection, new FakeDbTransaction(), request).AsTask());

        ex.Kind.Should().Be(ErrorCategory.Configuration);
    }

    [Fact]
    public async Task PostgreSqlProvider_BulkImportAsync_RejectsNonNpgsqlTransaction()
    {
        var provider = new PostgreSqlProvider();
        await using var connection = new NpgsqlConnection("Host=localhost;Database=postgres;Username=postgres;Password=postgres;");

        var request = CreateRequest();
        var ex = await Assert.ThrowsAsync<DatabaseException>(() =>
            provider.BulkImportAsync(connection, new FakeDbTransaction(), request).AsTask());

        ex.Kind.Should().Be(ErrorCategory.Configuration);
    }

    private static BulkImportRequest CreateRequest()
    {
        var table = new DataTable();
        table.Columns.Add("Id", typeof(int));
        table.Rows.Add(1);

        return new BulkImportRequest
        {
            DestinationTable = "SomeTable",
            SourceReader = table.CreateDataReader(),
            ColumnMappings = new[] { new BulkImportColumnMapping { SourceColumn = "Id", DestinationColumn = "Id" } }
        };
    }

    private sealed class FakeDbTransaction : DbTransaction
    {
        public override IsolationLevel IsolationLevel => IsolationLevel.ReadCommitted;
        protected override DbConnection DbConnection => throw new NotSupportedException();
        public override void Commit() => throw new NotSupportedException();
        public override void Rollback() => throw new NotSupportedException();
    }
}

