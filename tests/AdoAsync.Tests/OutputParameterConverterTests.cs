using System;
using AdoAsync.Execution;
using FluentAssertions;
using Xunit;

namespace AdoAsync.Tests;

public class OutputParameterConverterTests
{
    [Fact]
    public void Normalize_ConvertsDecimalToInt64()
    {
        // Oracle often returns numeric outputs as decimal.
        var value = 42.0m;

        var result = DbValueNormalizer.Normalize(value, DbDataType.Int64);

        result.Should().BeOfType<long>();
        result.Should().Be(42L);
    }

    [Fact]
    public void Normalize_ConvertsStringToGuid()
    {
        var guid = Guid.NewGuid();

        var result = DbValueNormalizer.Normalize(guid.ToString(), DbDataType.Guid);

        result.Should().BeOfType<Guid>();
        result.Should().Be(guid);
    }

    [Fact]
    public void Normalize_ConvertsDateTimeToDateTimeOffset()
    {
        var now = new DateTime(2024, 1, 1, 12, 30, 0, DateTimeKind.Utc);

        var result = DbValueNormalizer.Normalize(now, DbDataType.DateTimeOffset);

        result.Should().BeOfType<DateTimeOffset>();
        result.Should().Be(new DateTimeOffset(now));
    }

    [Fact]
    public void Normalize_ConvertsTimeStringToTimeSpan()
    {
        var result = DbValueNormalizer.Normalize("01:02:03", DbDataType.Time);

        result.Should().BeOfType<TimeSpan>();
        result.Should().Be(TimeSpan.FromHours(1) + TimeSpan.FromMinutes(2) + TimeSpan.FromSeconds(3));
    }

    [Fact]
    public void Normalize_ReturnsOriginalValueWhenConversionFails()
    {
        var value = new object();

        var result = DbValueNormalizer.Normalize(value, DbDataType.Int32);

        result.Should().BeSameAs(value);
    }
}
