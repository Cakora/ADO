using System.Collections.Generic;
using System.Data;

namespace AdoAsync.Extensions.Execution;

internal static class DataTableExtensions
{
    /// <summary>Project DataRow items to a list using the provided mapper.</summary>
    /// <param name="table">Table to project.</param>
    /// <param name="map">Row mapping function.</param>
    /// <returns>List of mapped items.</returns>
    public static List<T> ToList<T>(this DataTable table, Func<DataRow, T> map)
    {
        var results = new List<T>(table.Rows.Count);
        // Use indexed access to avoid the foreach enumerator overhead on DataRowCollection.
        for (var i = 0; i < table.Rows.Count; i++)
        {
            results.Add(map(table.Rows[i]));
        }
        return results;
    }
}
