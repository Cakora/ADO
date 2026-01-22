using System.Collections.Generic;
using System.Data;
using AdoAsync.Extensions.Execution;
using FluentAssertions;
using Xunit;

namespace AdoAsync.Tests;

public class OutputParameterExtensionsTests
{
    [Fact]
    public void GetOutputParameters_DataTable_ReturnsDictionaryWhenPresent()
    {
        var table = new DataTable();
        var outputs = new Dictionary<string, object?> { ["p1"] = 1 };
        table.ExtendedProperties["OutputParameters"] = outputs;

        var result = table.GetOutputParameters();

        result.Should().BeSameAs(outputs);
    }

    [Fact]
    public void GetOutputParameters_DataTable_ReturnsNullWhenMissing()
    {
        var table = new DataTable();

        var result = table.GetOutputParameters();

        result.Should().BeNull();
    }

    [Fact]
    public void GetOutputParameters_DataSet_ReturnsDictionaryWhenPresent()
    {
        var dataSet = new DataSet();
        var outputs = new Dictionary<string, object?> { ["p1"] = 1 };
        dataSet.ExtendedProperties["OutputParameters"] = outputs;

        var result = dataSet.GetOutputParameters();

        result.Should().BeSameAs(outputs);
    }

    [Fact]
    public void GetOutputParameters_DataSet_ReturnsNullWhenMissing()
    {
        var dataSet = new DataSet();

        var result = dataSet.GetOutputParameters();

        result.Should().BeNull();
    }
}
