using System;
using System.Collections.Generic;
using System.Collections.Frozen;
using System.Threading;
using System.Threading.Tasks;

namespace AdoAsync.Extensions.Execution;

/// <summary>Materializers for <see cref="IAsyncEnumerable{T}"/>.</summary>
public static class AsyncEnumerableMaterializerExtensions
{
    /*
     * Collection selection cheat-sheet (memory-first)
     * - Sequential: IAsyncEnumerable<T>
     * - One filter: IEnumerable<T>
     * - Reuse: List<T>
     * - Fixed size: T[]
     * - Group once: ILookup<TKey,T>
     * - Group many: FrozenDictionary<TKey,T>
     * - Oracle: DataTable → convert → dispose
     */

    /// <summary>
    /// Materialize an async stream into a <see cref="List{T}"/>.
     /// </summary>
    /// <remarks>
    /// Purpose:
    /// Materialize a stream once to enable repeated access (indexing/re-enumeration).
    ///
    /// When to use:
    /// - You need to enumerate multiple times or index by position
    ///
    /// When NOT to use:
    /// - One-pass processing is sufficient (prefer streaming)
    /// - Very large streams (high memory usage)
    ///
    /// Lifetime / Ownership:
    /// - Source owner: caller owns <paramref name="source"/> and its underlying resources.
    /// - Result owner: caller owns the returned <see cref="List{T}"/>.
    /// - Source disposal: dispose the upstream owner (for example, a reader/executor) after enumeration completes.
    /// - Result release: release by dropping references to the returned list (GC).
    /// </remarks>
    public static async ValueTask<List<T>> ToListAsync<T>(this IAsyncEnumerable<T> source, CancellationToken cancellationToken = default)
    {
        if (source is null) throw new ArgumentNullException(nameof(source));

        var results = new List<T>();
        await foreach (var item in source.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            results.Add(item);
        }

        return results;
    }

    /// <summary>
    /// Materialize an async stream into an array.
    /// </summary>
    /// <remarks>
    /// Purpose:
    /// Materialize a stream into a compact array representation.
    ///
    /// When to use:
    /// - You want a compact, array-backed result and can hold all items in memory
    ///
    /// When NOT to use:
    /// - One-pass processing is sufficient (prefer streaming)
    /// - Very large streams (high memory usage)
    ///
    /// Lifetime / Ownership:
    /// - Source owner: caller owns <paramref name="source"/> and its underlying resources.
    /// - Result owner: caller owns the returned array.
    /// - Source disposal: dispose the upstream owner after enumeration completes.
    /// - Result release: release by dropping references to the returned array (GC).
    /// </remarks>
    public static async ValueTask<T[]> ToArrayAsync<T>(this IAsyncEnumerable<T> source, CancellationToken cancellationToken = default)
    {
        var list = await source.ToListAsync(cancellationToken).ConfigureAwait(false);
        return list.ToArray();
    }

    /// <summary>
    /// Materialize an async stream into a <see cref="FrozenDictionary{TKey,TValue}"/>.
    /// </summary>
    /// <remarks>
    /// Purpose:
    /// Build a frozen dictionary for repeated lookups after a single materialization pass.
    ///
    /// When to use:
    /// - You build once and perform repeated lookups
    ///
    /// When NOT to use:
    /// - You only need single-pass filtering (prefer streaming)
    /// - Keys are not unique (this throws)
    ///
    /// Lifetime / Ownership:
    /// - Source owner: caller owns <paramref name="source"/> and its underlying resources.
    /// - Result owner: caller owns the returned <see cref="FrozenDictionary{TKey,TValue}"/>.
    /// - Source disposal: dispose the upstream owner after enumeration completes.
    /// - Result release: release by dropping references to the returned dictionary (GC).
    /// </remarks>
    public static async ValueTask<FrozenDictionary<TKey, TValue>> ToFrozenDictionaryAsync<TSource, TKey, TValue>(
        this IAsyncEnumerable<TSource> source,
        Func<TSource, TKey> keySelector,
        Func<TSource, TValue> valueSelector,
        IEqualityComparer<TKey>? comparer = null,
        CancellationToken cancellationToken = default)
        where TKey : notnull
    {
        if (source is null) throw new ArgumentNullException(nameof(source));
        if (keySelector is null) throw new ArgumentNullException(nameof(keySelector));
        if (valueSelector is null) throw new ArgumentNullException(nameof(valueSelector));

        var dictionary = new Dictionary<TKey, TValue>(comparer);
        await foreach (var item in source.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            var key = keySelector(item);
            if (!dictionary.TryAdd(key, valueSelector(item)))
            {
                throw new ArgumentException($"Duplicate key '{key}'.", nameof(keySelector));
            }
        }

        return dictionary.ToFrozenDictionary(comparer);
    }

    /// <summary>
    /// Materialize an async stream into an <see cref="ILookup{TKey,TElement}"/> backed by arrays (no double-materialization).
    /// </summary>
    /// <remarks>
    /// Purpose:
    /// Group items by key for repeated lookup/enumeration of groups after buffering.
    ///
    /// When to use:
    /// - You need repeated access to grouped items by key
    ///
    /// When NOT to use:
    /// - One-pass processing is sufficient (prefer streaming)
    /// - Very large streams (high memory usage)
    ///
    /// Lifetime / Ownership:
    /// - Source owner: caller owns <paramref name="source"/> and its underlying resources.
    /// - Result owner: caller owns the returned <see cref="ILookup{TKey,TElement}"/>.
    /// - Source disposal: dispose the upstream owner after enumeration completes.
    /// - Result release: release by dropping references to the returned lookup (GC).
    /// </remarks>
    public static async ValueTask<ILookup<TKey, TElement>> ToLookupAsync<TSource, TKey, TElement>(
        this IAsyncEnumerable<TSource> source,
        Func<TSource, TKey> keySelector,
        Func<TSource, TElement> elementSelector,
        IEqualityComparer<TKey>? comparer = null,
        CancellationToken cancellationToken = default)
        where TKey : notnull
    {
        if (source is null) throw new ArgumentNullException(nameof(source));
        if (keySelector is null) throw new ArgumentNullException(nameof(keySelector));
        if (elementSelector is null) throw new ArgumentNullException(nameof(elementSelector));

        var groups = new Dictionary<TKey, List<TElement>>(comparer);
        await foreach (var item in source.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            var key = keySelector(item);
            if (!groups.TryGetValue(key, out var list))
            {
                list = new List<TElement>();
                groups[key] = list;
            }

            list.Add(elementSelector(item));
        }

        return LookupImpl<TKey, TElement>.From(groups, comparer);
    }

    private sealed class LookupImpl<TKey, TElement> : ILookup<TKey, TElement>
        where TKey : notnull
    {
        private readonly Dictionary<TKey, TElement[]> _groups;

        private LookupImpl(Dictionary<TKey, TElement[]> groups)
        {
            _groups = groups;
        }

        public static LookupImpl<TKey, TElement> From(Dictionary<TKey, List<TElement>> groups, IEqualityComparer<TKey>? comparer)
        {
            var resolvedComparer = comparer ?? EqualityComparer<TKey>.Default;
            var arrays = new Dictionary<TKey, TElement[]>(groups.Count, resolvedComparer);
            foreach (var pair in groups)
            {
                arrays[pair.Key] = pair.Value.ToArray();
            }

            return new LookupImpl<TKey, TElement>(arrays);
        }

        public bool Contains(TKey key) => _groups.ContainsKey(key);

        public int Count => _groups.Count;

        public IEnumerable<TElement> this[TKey key] =>
            _groups.TryGetValue(key, out var values) ? values : Array.Empty<TElement>();

        public IEnumerator<IGrouping<TKey, TElement>> GetEnumerator()
        {
            foreach (var pair in _groups)
            {
                yield return new GroupingImpl(pair.Key, pair.Value);
            }
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

        private sealed class GroupingImpl : IGrouping<TKey, TElement>
        {
            private readonly TElement[] _values;

            public GroupingImpl(TKey key, TElement[] values)
            {
                Key = key;
                _values = values;
            }

            public TKey Key { get; }

            public IEnumerator<TElement> GetEnumerator() => ((IEnumerable<TElement>)_values).GetEnumerator();

            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
        }
    }
}
