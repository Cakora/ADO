using System;
using System.Collections.Generic;
using System.Globalization;
using AdoAsync.Common;
using FluentAssertions;
using Xunit;

namespace AdoAsync.Tests;

public sealed class XmlExtensionsTests
{
    private sealed record Sample(
        int Id,
        DateTime CreatedAtUtc,
        DateTimeOffset UpdatedAtOffset,
        TimeSpan Duration,
        Guid CorrelationId);

    [Fact]
    public void ToXml_FormatsDateTimeDateTimeOffsetAndTimeSpan_InInvariantRoundTrip()
    {
        var created = new DateTime(2024, 01, 02, 03, 04, 05, DateTimeKind.Utc);
        var updated = new DateTimeOffset(2024, 02, 03, 04, 05, 06, TimeSpan.Zero);
        var duration = TimeSpan.FromSeconds(62);
        var correlationId = Guid.Parse("11111111-2222-3333-4444-555555555555");

        var items = new List<Sample>
        {
            new(1, created, updated, duration, correlationId)
        };

        var xml = items.ToXml(rootElementName: "Items", itemElementName: "Item");

        xml.Should().Contain("<CreatedAtUtc>2024-01-02T03:04:05.0000000Z</CreatedAtUtc>");
        xml.Should().Contain("<UpdatedAtOffset>2024-02-03T04:05:06.0000000+00:00</UpdatedAtOffset>");
        xml.Should().Contain("<Duration>00:01:02</Duration>");
        xml.Should().Contain("<CorrelationId>11111111-2222-3333-4444-555555555555</CorrelationId>");
    }

    [Fact]
    public void ToXml_UsesInvariantFormatting_ForDecimals()
    {
        var items = new[] { new PriceRow(1.23m) };

        var xml = items.ToXml("Rows", "Row");

        xml.Should().Contain("<Amount>1.23</Amount>");
        xml.Should().NotContain("<Amount>1,23</Amount>");
    }

    [Fact]
    public void ToXml_SkipsNullPropertyValues()
    {
        var items = new[] { new NullableRow(null) };

        var xml = items.ToXml("Rows", "Row");

        xml.Should().NotContain("<Text>");
    }

    private sealed record PriceRow(decimal Amount);
    private sealed record NullableRow(string? Text);
}

