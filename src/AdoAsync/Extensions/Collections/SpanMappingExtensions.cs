using System;

namespace AdoAsync.Extensions.Execution;

/// <summary>Span-based mapping helpers for array-backed data.</summary>
public static class SpanMappingExtensions
{
    /// <summary>
    /// Project an input array to an output array using a span-based loop.
    /// </summary>
    /// <remarks>
    /// Purpose:
    /// Transform array-backed data with a tight loop (minimal overhead, no LINQ allocations).
    ///
    /// When to use:
    /// - Data is already materialized as arrays and you want a low-overhead projection
    ///
    /// When NOT to use:
    /// - You can keep data streaming (avoid materializing first)
    /// - You need grouping/indexing (use collection transformers)
    ///
    /// Lifetime / Ownership:
    /// - Source owner: caller owns <paramref name="source"/>.
    /// - Result owner: caller owns the returned array.
    /// - Source disposal: not applicable (managed array).
    /// - Result release: release by dropping references to returned array (GC).
    /// </remarks>
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
