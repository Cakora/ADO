using System.Data;
using AdoAsync.Extensions.Execution;
using FluentAssertions;
using Xunit;

namespace AdoAsync.Tests;

public class DataRowExtensionsTests
{
    [Fact]
    public void SafeGet_ReturnsNullForNull()
    {
        var table = new DataTable();
        table.Columns.Add("name", typeof(string));
        var row = table.NewRow();
        row["name"] = DBNull.Value;
        table.Rows.Add(row);

        row.SafeGet<string>("name").Should().BeNull();
    }

    [Fact]
    public void SafeGet_ThrowsForNullNonNullableValueType()
    {
        var table = new DataTable();
        table.Columns.Add("count", typeof(int));
        var row = table.NewRow();
        row["count"] = DBNull.Value;
        table.Rows.Add(row);

        row.Invoking(r => r.SafeGet<int>("count"))
            .Should()
            .Throw<InvalidOperationException>();
    }

    [Fact]
    public void SafeGet_ReturnsNullForNullableValueType()
    {
        var table = new DataTable();
        table.Columns.Add("count", typeof(int));
        var row = table.NewRow();
        row["count"] = DBNull.Value;
        table.Rows.Add(row);

        row.SafeGet<int?>("count").Should().BeNull();
    }

    [Fact]
    public void SafeGet_ConvertsNumericTypes()
    {
        var table = new DataTable();
        table.Columns.Add("count", typeof(long));
        var row = table.NewRow();
        row["count"] = 5L;
        table.Rows.Add(row);

        row.SafeGet<int>("count").Should().Be(5);
        row.SafeGet<decimal>("count").Should().Be(5m);
    }

    [Fact]
    public void SafeGet_ConvertsStringToChar()
    {
        var table = new DataTable();
        table.Columns.Add("code", typeof(string));
        var row = table.NewRow();
        row["code"] = "A";
        table.Rows.Add(row);

        row.SafeGet<char>("code").Should().Be('A');
    }

    [Fact]
    public void SafeGet_ConvertsNumericToBool()
    {
        var table = new DataTable();
        table.Columns.Add("flag", typeof(int));
        var row = table.NewRow();
        row["flag"] = 1;
        table.Rows.Add(row);

        row.SafeGet<bool>("flag").Should().BeTrue();
    }

    [Fact]
    public void SafeGet_ConvertsStringToGuid()
    {
        var table = new DataTable();
        table.Columns.Add("id", typeof(string));
        var row = table.NewRow();
        var guid = Guid.NewGuid();
        row["id"] = guid.ToString();
        table.Rows.Add(row);

        row.SafeGet<Guid>("id").Should().Be(guid);
    }

    [Fact]
    public void SafeGet_ConvertsDateTimeToDateTimeOffset()
    {
        var table = new DataTable();
        table.Columns.Add("ts", typeof(DateTime));
        var row = table.NewRow();
        var now = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc);
        row["ts"] = now;
        table.Rows.Add(row);

        row.SafeGet<DateTimeOffset>("ts").Should().Be(new DateTimeOffset(now, TimeSpan.Zero));
    }

    private enum Status
    {
        Unknown = 0,
        Active = 1
    }

    [Fact]
    public void SafeGet_ConvertsIntToEnum()
    {
        var table = new DataTable();
        table.Columns.Add("status", typeof(int));
        var row = table.NewRow();
        row["status"] = 1;
        table.Rows.Add(row);

        row.SafeGet<Status>("status").Should().Be(Status.Active);
    }

    [Fact]
    public void SafeGet_ThrowsForMissingColumn()
    {
        var table = new DataTable();
        table.Columns.Add("name", typeof(string));
        var row = table.NewRow();
        row["name"] = "ok";
        table.Rows.Add(row);

        row.Invoking(r => r.SafeGet<string>("missing"))
            .Should()
            .Throw<ArgumentException>();
    }

}
