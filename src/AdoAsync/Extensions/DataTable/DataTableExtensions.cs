using System;
using System.Collections.Generic;
using System.Data;

namespace AdoAsync.Extensions.Execution;

/// <summary>DataTable mapping helpers (buffered).</summary>
public static class DataTableExtensions
{
    /// <summary>
    /// Project DataRow items to a list using the provided mapper.
    /// </summary>
    /// <remarks>
    /// Purpose:
    /// Convert a buffered <see cref="DataTable"/> into a <see cref="List{T}"/> using a fast indexed loop.
    ///
    /// When to use:
    /// - Oracle / RefCursor / buffered paths where streaming is not possible
    /// - When you need repeated filtering/grouping after materialization
    ///
    /// When NOT to use:
    /// - Large datasets (high memory)
    /// - SQL Server / PostgreSQL when streaming is available (prefer streaming)
    ///
    /// Lifetime / Ownership:
    /// - Source owner: caller owns <paramref name="table"/>.
    /// - Result owner: caller owns the returned <see cref="List{T}"/>.
    /// - Source disposal: dispose/release <paramref name="table"/> as soon as mapping is complete.
    /// - Result release: release by dropping references to the returned list (GC).
    /// </remarks>
    public static List<T> ToList<T>(this DataTable table, Func<DataRow, T> map)
    {
        if (table is null) throw new ArgumentNullException(nameof(table));
        if (map is null) throw new ArgumentNullException(nameof(map));

        var results = new List<T>(table.Rows.Count);
        for (var i = 0; i < table.Rows.Count; i++)
        {
            results.Add(map(table.Rows[i]));
        }

        return results;
    }

    /// <summary>
    /// Project DataRow items to an array using the provided mapper.
    /// </summary>
    /// <remarks>
    /// Purpose:
    /// Convert a buffered <see cref="DataTable"/> into a compact array representation in one pass.
    ///
    /// When to use:
    /// - Oracle / RefCursor / buffered paths where streaming is not possible
    /// - When you want predictable memory and fast iteration (array-backed)
    ///
    /// When NOT to use:
    /// - When you need incremental growth (prefer <see cref="ToList{T}"/>)
    /// - SQL Server / PostgreSQL when streaming is available (prefer streaming)
    ///
    /// Lifetime / Ownership:
    /// - Source owner: caller owns <paramref name="table"/>.
    /// - Result owner: caller owns the returned array.
    /// - Source disposal: dispose/release <paramref name="table"/> as soon as mapping is complete.
    /// - Result release: release by dropping references to the returned array (GC).
    /// </remarks>
    public static T[] ToArray<T>(this DataTable table, Func<DataRow, T> map)
    {
        if (table is null) throw new ArgumentNullException(nameof(table));
        if (map is null) throw new ArgumentNullException(nameof(map));

        var rows = table.Rows;
        var results = new T[rows.Count];
        for (var i = 0; i < rows.Count; i++)
        {
            results[i] = map(rows[i]);
        }

        return results;
    }
}
