using System;
using System.Collections.Generic;
using System.Data;

namespace AdoAsync.Extensions.Execution;

/// <summary>DataSet conversion helpers (buffered).</summary>
public static class DataSetExtensions
{
    /// <summary>
    /// Convert a buffered <see cref="DataSet"/> into a <see cref="MultiResult"/> with optional output parameters.
    /// </summary>
    /// <remarks>
    /// Purpose:
    /// Wrap a buffered <see cref="DataSet"/> into a <see cref="MultiResult"/> for consistent downstream mapping.
    ///
    /// When to use:
    /// - Buffered multi-result execution where you already have a <see cref="DataSet"/>
    /// - Oracle / refcursor paths that must materialize results
    ///
    /// When NOT to use:
    /// - Streaming scenarios (SQL Server/PostgreSQL) where a one-pass read is sufficient
    ///
    /// Notes:
    /// - This method does not copy tables; it collects references.
    /// - Dispose the underlying <see cref="DataTable"/> instances as soon as mapping is complete.
    ///
    /// Lifetime / Ownership:
    /// - Source owner: caller owns <paramref name="dataSet"/> and its tables.
    /// - Result owner: caller owns the returned <see cref="MultiResult"/> wrapper.
    /// - Source disposal: dispose/release tables after mapping; do not keep DataTable references long-term.
    /// - Result release: release by dropping references to the returned wrapper and mapped results (GC).
    /// </remarks>
    public static MultiResult ToMultiResult(this DataSet dataSet, IReadOnlyDictionary<string, object?>? outputParameters = null)
    {
        if (dataSet is null) throw new ArgumentNullException(nameof(dataSet));

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
