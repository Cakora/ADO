using System;
using System.Collections.Generic;
using System.Data;

namespace AdoAsync.Extensions.Execution;

/// <summary>Helpers to retrieve output parameters from buffered results.</summary>
public static class OutputParameterExtensions
{
    /// <summary>Gets output parameters stored in DataTable.ExtendedProperties, if present.</summary>
    /// <param name="table">DataTable returned from QueryTableAsync.</param>
    /// <returns>Output parameters dictionary or null when none exist.</returns>
    public static IReadOnlyDictionary<string, object?>? GetOutputParameters(this DataTable table)
    {
        ArgumentNullException.ThrowIfNull(table);
        return TryGetOutputs(table.ExtendedProperties);
    }

    /// <summary>Gets output parameters stored in DataSet.ExtendedProperties, if present.</summary>
    /// <param name="dataSet">DataSet returned from ExecuteDataSetAsync.</param>
    /// <returns>Output parameters dictionary or null when none exist.</returns>
    public static IReadOnlyDictionary<string, object?>? GetOutputParameters(this DataSet dataSet)
    {
        ArgumentNullException.ThrowIfNull(dataSet);
        return TryGetOutputs(dataSet.ExtendedProperties);
    }

    private static IReadOnlyDictionary<string, object?>? TryGetOutputs(PropertyCollection properties)
    {
        if (properties.Contains("OutputParameters") &&
            properties["OutputParameters"] is IReadOnlyDictionary<string, object?> outputs)
        {
            return outputs;
        }

        return null;
    }
}
