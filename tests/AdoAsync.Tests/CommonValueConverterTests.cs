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
}
