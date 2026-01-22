using System.Collections.Generic;
using System.Data;
using AdoAsync.Extensions.Execution;
using FluentAssertions;
using Xunit;

namespace AdoAsync.Tests;

public class DataTableOutputExtensionsTests
{
    [Fact]
    public void GetOutputParameters_ReturnsDictionaryWhenPresent()
    {
        var table = new DataTable();
        var outputs = new Dictionary<string, object?> { ["p1"] = 1 };
        table.ExtendedProperties["OutputParameters"] = outputs;

        var result = table.GetOutputParameters();

        result.Should().BeSameAs(outputs);
    }

    [Fact]
    public void GetOutputParameters_ReturnsNullWhenMissing()
    {
        var table = new DataTable();

        var result = table.GetOutputParameters();

        result.Should().BeNull();
    }
}
