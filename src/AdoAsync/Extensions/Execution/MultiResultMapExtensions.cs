using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Data;

namespace AdoAsync.Extensions.Execution;

/// <summary>Mapping helpers for buffered multi-result shapes.</summary>
internal static class MultiResultMapExtensions
{
    /// <summary>Map all tables in a DataSet to a list of mapped rows using a single mapper.</summary>
    /// <param name="dataSet">Buffered DataSet.</param>
    /// <param name="map">Row mapper applied to every table.</param>
    /// <returns>Mapped rows per table.</returns>
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

    /// <summary>Map tables in a MultiResult using per-table mappers.</summary>
    /// <param name="multiResult">Buffered multi-result.</param>
    /// <param name="mappers">Row mappers aligned to tables.</param>
    /// <returns>Mapped rows per table.</returns>
    public static IReadOnlyList<List<T>> MapTables<T>(this MultiResult multiResult, params Func<DataRow, T>[] mappers)
    {
        if (multiResult is null) throw new ArgumentNullException(nameof(multiResult));
        if (mappers is null) throw new ArgumentNullException(nameof(mappers));

        var tables = multiResult.Tables ?? Array.Empty<DataTable>();
        var results = new List<List<T>>(tables.Count);

        for (var i = 0; i < tables.Count; i++)
        {
            var mapper = i < mappers.Length ? mappers[i] : null;
            if (mapper is null)
            {
                throw new ArgumentException("Mapper is required for each table.", nameof(mappers));
            }

            var table = tables[i];
            var list = new List<T>(table.Rows.Count);
            for (var rowIndex = 0; rowIndex < table.Rows.Count; rowIndex++)
            {
                // Keep mapping lightweight for buffered tables (no LINQ allocations).
                list.Add(mapper(table.Rows[rowIndex]));
            }
            results.Add(list);
        }

        return results;
    }

    /// <summary>Map tables in a MultiResult using per-table mappers and a shared collection factory.</summary>
    /// <param name="multiResult">Buffered multi-result.</param>
    /// <param name="collectionFactory">Factory to create a collection per table (receives row count).</param>
    /// <param name="mappers">Row mappers aligned to tables.</param>
    /// <typeparam name="T">Mapped row type.</typeparam>
    /// <typeparam name="TCollection">Collection type (must accept Add).</typeparam>
    /// <example>
    /// Map to arrays with per-table mappers:
    /// <code>
    /// var arrays = multiResult.MapTables(
    ///     size => new Foo[size],
    ///     row => new Foo(row.Field&lt;int&gt;("Id")),
    ///     row => new Foo(row.Field&lt;int&gt;("Id2")));
    /// </code>
    /// Map to immutable arrays:
    /// <code>
    /// var immutable = multiResult.MapTables(
    ///     size => new List&lt;Foo&gt;(size),
    ///     row => new Foo(row.Field&lt;int&gt;("Id")),
    ///     row => new Foo(row.Field&lt;int&gt;("Id2")))
    ///     .Select(list => list.ToImmutableArray())
    ///     .ToList();
    /// </code>
    /// Map to ReadOnlyCollection:
    /// <code>
    /// var readOnly = multiResult.MapTables(
    ///     size => new List&lt;Foo&gt;(size),
    ///     row => new Foo(row.Field&lt;int&gt;("Id")),
    ///     row => new Foo(row.Field&lt;int&gt;("Id2")))
    ///     .Select(list => list.AsReadOnly())
    ///     .ToList();
    /// </code>
    /// </example>
    /// <returns>Mapped collections per table.</returns>
    public static IReadOnlyList<TCollection> MapTables<T, TCollection>(this MultiResult multiResult, Func<int, TCollection> collectionFactory, params Func<DataRow, T>[] mappers)
        where TCollection : ICollection<T>
    {
        if (multiResult is null) throw new ArgumentNullException(nameof(multiResult));
        if (collectionFactory is null) throw new ArgumentNullException(nameof(collectionFactory));
        if (mappers is null) throw new ArgumentNullException(nameof(mappers));

        var tables = multiResult.Tables ?? Array.Empty<DataTable>();
        var results = new List<TCollection>(tables.Count);

        for (var i = 0; i < tables.Count; i++)
        {
            var mapper = i < mappers.Length ? mappers[i] : null;
            if (mapper is null)
            {
                throw new ArgumentException("Mapper is required for each table.", nameof(mappers));
            }

            var table = tables[i];
            var collection = collectionFactory(table.Rows.Count);
            for (var rowIndex = 0; rowIndex < table.Rows.Count; rowIndex++)
            {
                // Write directly into the caller-provided collection to avoid extra allocations.
                collection.Add(mapper(table.Rows[rowIndex]));
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

    /// <summary>Map tables in a MultiResult to arrays for maximum read performance.</summary>
    /// <param name="multiResult">Buffered multi-result.</param>
    /// <param name="mappers">Row mappers aligned to tables.</param>
    /// <typeparam name="T">Mapped row type.</typeparam>
    /// <returns>Array per table.</returns>
    /// <example>
    /// Map to arrays with per-table mappers:
    /// <code>
    /// var arrays = multiResult.MapTablesToArrays(
    ///     row => new Foo(row.Field&lt;int&gt;("Id")),
    ///     row => new Foo(row.Field&lt;int&gt;("Id2")));
    /// </code>
    /// </example>
    public static IReadOnlyList<T[]> MapTablesToArrays<T>(this MultiResult multiResult, params Func<DataRow, T>[] mappers)
    {
        if (multiResult is null) throw new ArgumentNullException(nameof(multiResult));
        if (mappers is null) throw new ArgumentNullException(nameof(mappers));

        var tables = multiResult.Tables ?? Array.Empty<DataTable>();
        var results = new List<T[]>(tables.Count);

        for (var i = 0; i < tables.Count; i++)
        {
            var mapper = i < mappers.Length ? mappers[i] : null;
            if (mapper is null)
            {
                throw new ArgumentException("Mapper is required for each table.", nameof(mappers));
            }

            var table = tables[i];
            var array = new T[table.Rows.Count];
            for (var rowIndex = 0; rowIndex < table.Rows.Count; rowIndex++)
            {
                array[rowIndex] = mapper(table.Rows[rowIndex]);
            }
            results.Add(array);
        }

        return results;
    }
}
