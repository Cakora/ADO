using System;

namespace AdoAsync.Extensions.Execution;

/// <summary>Span-based mapping helpers for array-backed data.</summary>
public static class SpanMappingExtensions
{
    /// <summary>Project an input array to an output array using a span-based loop.</summary>
    /// <param name="source">Source array.</param>
    /// <param name="map">Mapper applied to each element.</param>
    /// <typeparam name="TSource">Source element type.</typeparam>
    /// <typeparam name="TDest">Destination element type.</typeparam>
    /// <returns>Mapped array.</returns>
    /// <example>
    /// Apply after mapping DataTables to arrays:
    /// <code>
    /// var arrays = dataSet.MapTablesToArrays(row => new Foo(row.Field&lt;int&gt;("Id")));
    /// var projected = arrays[0].MapToArray(foo => new Bar(foo.Id));
    /// </code>
    /// </example>
    public static TDest[] MapToArray<TSource, TDest>(this TSource[] source, Func<TSource, TDest> map)
    {
        if (source is null) throw new ArgumentNullException(nameof(source));
        if (map is null) throw new ArgumentNullException(nameof(map));

        var dest = new TDest[source.Length];
        var sourceSpan = source.AsSpan();
        var destSpan = dest.AsSpan();
        for (var i = 0; i < sourceSpan.Length; i++)
        {
            destSpan[i] = map(sourceSpan[i]);
        }

        return dest;
    }
}
