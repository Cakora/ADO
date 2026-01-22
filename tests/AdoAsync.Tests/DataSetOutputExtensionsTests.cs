using System.Collections.Generic;
using System.Data;
using AdoAsync.Extensions.Execution;
using FluentAssertions;
using Xunit;

namespace AdoAsync.Tests;

public class DataSetOutputExtensionsTests
{
    [Fact]
    public void GetOutputParameters_ReturnsDictionaryWhenPresent()
    {
        var dataSet = new DataSet();
        var outputs = new Dictionary<string, object?> { ["p1"] = 1 };
        dataSet.ExtendedProperties["OutputParameters"] = outputs;

        var result = dataSet.GetOutputParameters();

        result.Should().BeSameAs(outputs);
    }

    [Fact]
    public void GetOutputParameters_ReturnsNullWhenMissing()
    {
        var dataSet = new DataSet();

        var result = dataSet.GetOutputParameters();

        result.Should().BeNull();
    }
}
