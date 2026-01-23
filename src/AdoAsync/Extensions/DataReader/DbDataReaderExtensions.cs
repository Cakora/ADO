using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Runtime.CompilerServices;
using System.Threading;

namespace AdoAsync.Extensions.Execution;

/// <summary>DbDataReader helpers for sequential, low-allocation access.</summary>
public static class DbDataReaderExtensions
{
    /// <summary>
        /// Streams records from a <see cref="DbDataReader"/> using sequential <c>ReadAsync</c> access.
    /// </summary>
    /// <remarks>
        /// Purpose:
        /// Stream rows with the lowest possible memory usage (no buffering).
        ///
        /// When to use:
        /// - SQL Server / PostgreSQL streaming paths
        /// - One-pass processing (map/validate/write) with sequential access
        ///
        /// When NOT to use:
        /// - When you need grouping, repeated filtering, or random access (materialize instead)
        /// - Oracle (streaming is not supported)
        ///
        /// Lifetime / Ownership:
        /// - Source owner: caller owns <paramref name="reader"/> (and must keep it open while enumerating).
        /// - Result owner: caller owns the returned async enumeration.
        /// - Source disposal: dispose/close <paramref name="reader"/> (or its owning wrapper) after enumeration completes.
        /// - Result release: release by ending enumeration and dropping references (GC).
    /// </remarks>
    public static IAsyncEnumerable<IDataRecord> StreamRecordsAsync(this DbDataReader reader, CancellationToken cancellationToken)
    {
        if (reader is null) throw new ArgumentNullException(nameof(reader));

        return StreamRecordsCore(reader, cancellationToken);
    }

    private static async IAsyncEnumerable<IDataRecord> StreamRecordsCore(DbDataReader reader, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return reader;
        }
    }

    /// <summary>
        /// Streams records and maps each row immediately (no buffering).
    /// </summary>
    /// <remarks>
        /// Purpose:
        /// Stream rows and project each row into <typeparamref name="T"/> without allocating intermediate collections.
        ///
        /// When to use:
        /// - SQL Server / PostgreSQL streaming paths
        /// - One-pass mapping into domain types
        ///
        /// When NOT to use:
        /// - When you need grouping/repeated filtering (materialize instead)
        /// - Oracle (streaming is not supported)
        ///
        /// Lifetime / Ownership:
        /// - Source owner: caller owns <paramref name="reader"/> (and must keep it open while enumerating).
        /// - Result owner: caller owns the returned async enumeration.
        /// - Source disposal: dispose/close <paramref name="reader"/> (or its owning wrapper) after enumeration completes.
        /// - Result release: release by ending enumeration and dropping references (GC).
    /// </remarks>
    public static IAsyncEnumerable<T> StreamAsync<T>(
        this DbDataReader reader,
        Func<IDataRecord, T> map,
        CancellationToken cancellationToken)
    {
        if (reader is null) throw new ArgumentNullException(nameof(reader));
        if (map is null) throw new ArgumentNullException(nameof(map));

        return StreamCore(reader, map, cancellationToken);
    }

    private static async IAsyncEnumerable<T> StreamCore<T>(
        DbDataReader reader,
        Func<IDataRecord, T> map,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return map(reader);
        }
    }
}
