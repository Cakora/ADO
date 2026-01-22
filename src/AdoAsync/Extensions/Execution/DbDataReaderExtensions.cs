using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Runtime.CompilerServices;
using System.Threading;

namespace AdoAsync.Extensions.Execution;

internal static class DbDataReaderExtensions
{
    /// <summary>Materialize all rows from a reader into a list using the provided mapper.</summary>
    /// <param name="reader">Reader to enumerate.</param>
    /// <param name="map">Row mapping function.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of mapped items.</returns>
    public static async ValueTask<List<T>> ToListAsync<T>(this DbDataReader reader, Func<IDataRecord, T> map, CancellationToken cancellationToken)
    {
        var results = new List<T>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            cancellationToken.ThrowIfCancellationRequested();
            results.Add(map(reader));
        }
        return results;
    }

    /// <summary>Stream records from a reader using ReadAsync.</summary>
    /// <param name="reader">Reader to enumerate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Async stream of IDataRecord.</returns>
    public static async IAsyncEnumerable<IDataRecord> StreamRecordsAsync(this DbDataReader reader, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return reader;
        }
    }
}
