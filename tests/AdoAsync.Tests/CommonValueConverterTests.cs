using System;
using System.Data;
using AdoAsync.Common;
using FluentAssertions;
using Xunit;

namespace AdoAsync.Tests;

public class CommonValueConverterTests
{
    private enum SampleStatus
    {
        None = 0,
        One = 1
    }

    [Fact]
    public void DataRecord_Get_ConvertsGuidFromBytes()
    {
        var guid = Guid.NewGuid();
        var table = new DataTable();
        table.Columns.Add("Id", typeof(byte[]));
        table.Rows.Add(guid.ToByteArray());

        using var reader = table.CreateDataReader();
        reader.Read().Should().BeTrue();

        var value = reader.Get<Guid>(0);

        value.Should().Be(guid);
    }

    [Fact]
    public void DataRecord_Get_ConvertsDateTimeToDateTimeOffset()
    {
        var now = new DateTime(2024, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        var table = new DataTable();
        table.Columns.Add("CreatedAt", typeof(DateTime));
        table.Rows.Add(now);

        using var reader = table.CreateDataReader();
        reader.Read().Should().BeTrue();

        var value = reader.Get<DateTimeOffset>(0);

        value.Should().Be(new DateTimeOffset(now));
    }

    [Fact]
    public void DataRecord_Get_ConvertsDateTimeOffsetFromString()
    {
        var text = "2024-01-02T03:04:05+00:00";
        var table = new DataTable();
        table.Columns.Add("CreatedAt", typeof(string));
        table.Rows.Add(text);

        using var reader = table.CreateDataReader();
        reader.Read().Should().BeTrue();

        var value = reader.Get<DateTimeOffset>(0);

        value.Should().Be(DateTimeOffset.Parse(text, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind));
    }

    [Fact]
    public void DataRecord_Get_ConvertsTimeSpanFromString()
    {
        var table = new DataTable();
        table.Columns.Add("Duration", typeof(string));
        table.Rows.Add("00:01:02");

        using var reader = table.CreateDataReader();
        reader.Read().Should().BeTrue();

        var value = reader.Get<TimeSpan>(0);

        value.Should().Be(TimeSpan.FromSeconds(62));
    }

    [Fact]
    public void DataRecord_Get_ConvertsEnumFromString()
    {
        var table = new DataTable();
        table.Columns.Add("Status", typeof(string));
        table.Rows.Add("One");

        using var reader = table.CreateDataReader();
        reader.Read().Should().BeTrue();

        var value = reader.Get<SampleStatus>(0);

        value.Should().Be(SampleStatus.One);
    }

    [Fact]
    public void DataRecord_Get_ConvertsNullableInt()
    {
        var table = new DataTable();
        table.Columns.Add("Count", typeof(int));
        table.Rows.Add(5);

        using var reader = table.CreateDataReader();
        reader.Read().Should().BeTrue();

        var value = reader.Get<int?>(0);

        value.Should().Be(5);
    }

    [Fact]
    public void DataTable_Normalize_ConvertsEnumToUnderlying()
    {
        var table = new DataTable();
        table.Columns.Add("Status", typeof(int));
        table.Rows.Add(1);

        var normalized = table.Normalize(new System.Collections.Generic.Dictionary<string, Type>
        {
            ["Status"] = typeof(SampleStatus)
        });

        normalized.Rows[0]["Status"].Should().Be(1);
    }

    [Fact]
    public void DataTable_Normalize_ConvertsEnumFromStringToUnderlying()
    {
        var table = new DataTable();
        table.Columns.Add("Status", typeof(string));
        table.Rows.Add("One");

        var normalized = table.Normalize(new System.Collections.Generic.Dictionary<string, Type>
        {
            ["Status"] = typeof(SampleStatus)
        });

        normalized.Columns["Status"]!.DataType.Should().Be(typeof(int));
        normalized.Rows[0]["Status"].Should().Be((int)SampleStatus.One);
    }

    [Fact]
    public void DataTable_Normalize_UsesUnderlyingTypeForNullable()
    {
        var table = new DataTable();
        table.Columns.Add("Count", typeof(int));
        table.Rows.Add(1);
        table.Rows.Add(DBNull.Value);

        var normalized = table.Normalize(new System.Collections.Generic.Dictionary<string, Type>
        {
            ["Count"] = typeof(int?)
        });

        normalized.Columns["Count"]!.DataType.Should().Be(typeof(int));
        normalized.Columns["Count"]!.AllowDBNull.Should().BeTrue();
        normalized.Rows[0]["Count"].Should().Be(1);
        normalized.Rows[1]["Count"].Should().Be(DBNull.Value);
    }
}
