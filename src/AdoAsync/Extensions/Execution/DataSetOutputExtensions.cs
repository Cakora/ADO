using System;
using System.Collections.Generic;
using System.Data;

namespace AdoAsync.Extensions.Execution;

/// <summary>Helpers to retrieve output parameters from a DataSet populated by ExecuteDataSetAsync.</summary>
public static class DataSetOutputExtensions
{
    /// <summary>Gets output parameters stored in DataSet.ExtendedProperties, if present.</summary>
    /// <param name="dataSet">DataSet returned from ExecuteDataSetAsync.</param>
    /// <returns>Output parameters dictionary or null when none exist.</returns>
    public static IReadOnlyDictionary<string, object?>? GetOutputParameters(this DataSet dataSet)
    {
        if (dataSet is null)
        {
            throw new ArgumentNullException(nameof(dataSet));
        }

        if (dataSet.ExtendedProperties.Contains("OutputParameters") &&
            dataSet.ExtendedProperties["OutputParameters"] is IReadOnlyDictionary<string, object?> outputs)
        {
            return outputs;
        }

        return null;
    }
}
