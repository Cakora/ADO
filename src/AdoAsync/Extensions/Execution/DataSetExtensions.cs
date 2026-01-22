using System.Collections.Generic;
using System.Data;

namespace AdoAsync.Extensions.Execution;

internal static class DataSetExtensions
{
    /// <summary>Convert a DataSet into a MultiResult with optional output parameters.</summary>
    /// <param name="dataSet">Buffered DataSet.</param>
    /// <param name="outputParameters">Output parameters captured during execution.</param>
    /// <returns>MultiResult containing tables and outputs.</returns>
    public static MultiResult ToMultiResult(this DataSet dataSet, IReadOnlyDictionary<string, object?>? outputParameters = null)
    {
        var tables = new List<DataTable>(dataSet.Tables.Count);
        foreach (DataTable table in dataSet.Tables)
        {
            tables.Add(table);
        }

        return new MultiResult
        {
            Tables = tables,
            OutputParameters = outputParameters
        };
    }
}
