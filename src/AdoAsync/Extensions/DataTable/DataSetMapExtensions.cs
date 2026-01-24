using System;
using System.Collections.Generic;
using System.Data;

namespace AdoAsync.Extensions.Execution;

/// <summary>Mapping helpers for buffered multi-result shapes.</summary>
public static class DataSetMapExtensions
{
    /// <summary>Map all tables in a DataSet to a list of mapped rows using a single mapper.</summary>
    /// <param name="dataSet">Buffered DataSet.</param>
    /// <param name="map">Row mapper applied to every table.</param>
    /// <returns>Mapped rows per table.</returns>
    /// <remarks>
    /// Purpose:
    /// Convert buffered multi-result tables into typed collections for repeated access.
    ///
    /// When to use:
    /// - Oracle / refcursor / buffered multi-result paths
    /// - When repeated filtering/grouping is required after buffering
    ///
    /// When NOT to use:
    /// - Streaming-first paths (SQL Server/PostgreSQL) where one-pass processing is sufficient
    /// - Very large result sets (high memory usage)
    ///
    /// Lifetime / Ownership:
    /// - Source owner: caller owns <paramref name="dataSet"/> and its tables.
    /// - Result owner: caller owns the returned mapped collections.
    /// - Source disposal: dispose/release tables after mapping; do not store DataTable references long-term.
    /// - Result release: release by dropping references to returned collections (GC).
    /// </remarks>
    public static IReadOnlyList<List<T>> MapTables<T>(this DataSet dataSet, Func<DataRow, T> map)
    {
        if (dataSet is null) throw new ArgumentNullException(nameof(dataSet));
        if (map is null) throw new ArgumentNullException(nameof(map));

        var results = new List<List<T>>(dataSet.Tables.Count);
        for (var i = 0; i < dataSet.Tables.Count; i++)
        {
            // Map each buffered table to a typed list and collect.
            results.Add(dataSet.Tables[i].ToList(map));
        }

        return results;
    }

    /// <summary>Map all tables in a DataSet to caller-provided collection types using a single mapper.</summary>
    /// <param name="dataSet">Buffered DataSet.</param>
    /// <param name="map">Row mapper applied to every table.</param>
    /// <param name="collectionFactory">Factory to create a collection per table (receives row count).</param>
    /// <typeparam name="T">Mapped row type.</typeparam>
    /// <typeparam name="TCollection">Collection type (must accept Add).</typeparam>
    /// <example>
    /// Map to arrays:
    /// <code>
    /// var arrays = dataSet.MapTables(
    ///     row => new Foo(row.Field&lt;int&gt;("Id")),
    ///     size => new Foo[size]);
    /// </code>
    /// Map to immutable arrays:
    /// <code>
    /// var immutable = dataSet.MapTables(
    ///     row => new Foo(row.Field&lt;int&gt;("Id")),
    ///     size => new List&lt;Foo&gt;(size)).Select(list => list.ToImmutableArray()).ToList();
    /// </code>
    /// Map to ReadOnlyCollection:
    /// <code>
    /// var readOnly = dataSet.MapTables(
    ///     row => new Foo(row.Field&lt;int&gt;("Id")),
    ///     size => new List&lt;Foo&gt;(size)).Select(list => list.AsReadOnly()).ToList();
    /// </code>
    /// </example>
    /// <returns>Mapped collections per table.</returns>
    /// <remarks>
    /// Purpose:
    /// Map buffered tables into caller-chosen collection types while avoiding intermediate allocations.
    ///
    /// When to use:
    /// - You need arrays/immutable/read-only collections after buffering
    /// - You want to control collection shape (memory vs API surface)
    ///
    /// When NOT to use:
    /// - Streaming scenarios
    /// - Very large result sets (high memory usage)
    ///
    /// Lifetime / Ownership:
    /// - Source owner: caller owns <paramref name="dataSet"/> and its tables.
    /// - Result owner: caller owns the returned collections.
    /// - Source disposal: dispose/release tables after mapping.
    /// - Result release: release by dropping references to returned collections (GC).
    /// </remarks>
    public static IReadOnlyList<TCollection> MapTables<T, TCollection>(this DataSet dataSet, Func<DataRow, T> map, Func<int, TCollection> collectionFactory)
        where TCollection : ICollection<T>
    {
        if (dataSet is null) throw new ArgumentNullException(nameof(dataSet));
        if (map is null) throw new ArgumentNullException(nameof(map));
        if (collectionFactory is null) throw new ArgumentNullException(nameof(collectionFactory));

        var results = new List<TCollection>(dataSet.Tables.Count);
        for (var i = 0; i < dataSet.Tables.Count; i++)
        {
            var table = dataSet.Tables[i];
            var collection = collectionFactory(table.Rows.Count);
            for (var rowIndex = 0; rowIndex < table.Rows.Count; rowIndex++)
            {
                // Write directly into the caller-provided collection to avoid extra allocations.
                collection.Add(map(table.Rows[rowIndex]));
            }
            results.Add(collection);
        }

        return results;
    }

    /// <summary>Map all tables in a DataSet to arrays for maximum read performance.</summary>
    /// <param name="dataSet">Buffered DataSet.</param>
    /// <param name="map">Row mapper applied to every table.</param>
    /// <typeparam name="T">Mapped row type.</typeparam>
    /// <returns>Array per table.</returns>
    /// <example>
    /// Map to arrays:
    /// <code>
    /// var arrays = dataSet.MapTablesToArrays(row => new Foo(row.Field&lt;int&gt;("Id")));
    /// </code>
    /// </example>
    /// <remarks>
    /// Purpose:
    /// Convert buffered tables into arrays to minimize per-row overhead and reduce list growth allocations.
    ///
    /// When to use:
    /// - You know results are fully buffered and you want compact representation
    ///
    /// When NOT to use:
    /// - When you need incremental growth or unknown counts (prefer List)
    /// - Streaming scenarios
    ///
    /// Lifetime / Ownership:
    /// - Source owner: caller owns <paramref name="dataSet"/> and its tables.
    /// - Result owner: caller owns the returned arrays.
    /// - Source disposal: dispose/release tables after mapping.
    /// - Result release: release by dropping references to arrays (GC).
    /// </remarks>
    public static IReadOnlyList<T[]> MapTablesToArrays<T>(this DataSet dataSet, Func<DataRow, T> map)
    {
        if (dataSet is null) throw new ArgumentNullException(nameof(dataSet));
        if (map is null) throw new ArgumentNullException(nameof(map));

        var results = new List<T[]>(dataSet.Tables.Count);
        for (var i = 0; i < dataSet.Tables.Count; i++)
        {
            var table = dataSet.Tables[i];
            var array = new T[table.Rows.Count];
            for (var rowIndex = 0; rowIndex < table.Rows.Count; rowIndex++)
            {
                array[rowIndex] = map(table.Rows[rowIndex]);
            }
            results.Add(array);
        }

        return results;
    }
}
