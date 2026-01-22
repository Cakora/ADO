using System;
using System.Collections.Generic;
using System.Data;

namespace AdoAsync.Extensions.Execution;

/// <summary>Helpers to retrieve output parameters from DataTable results.</summary>
public static class DataTableOutputExtensions
{
    /// <summary>Gets output parameters stored in DataTable.ExtendedProperties, if present.</summary>
    /// <param name="table">DataTable returned from QueryTableAsync.</param>
    /// <returns>Output parameters dictionary or null when none exist.</returns>
    public static IReadOnlyDictionary<string, object?>? GetOutputParameters(this DataTable table)
    {
        if (table is null)
        {
            throw new ArgumentNullException(nameof(table));
        }

        if (table.ExtendedProperties.Contains("OutputParameters") &&
            table.ExtendedProperties["OutputParameters"] is IReadOnlyDictionary<string, object?> outputs)
        {
            return outputs;
        }

        return null;
    }
}
