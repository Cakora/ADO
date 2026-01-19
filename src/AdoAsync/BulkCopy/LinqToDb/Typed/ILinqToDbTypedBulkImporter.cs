using System.Collections.Generic;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using AdoAsync.BulkCopy.LinqToDb.Common;

namespace AdoAsync.BulkCopy.LinqToDb.Typed;

internal interface ILinqToDbTypedBulkImporter
{
    ValueTask<int> BulkImportAsync<T>(
        DbConnection connection,
        DbTransaction? transaction,
        IEnumerable<T> items,
        LinqToDbBulkOptions options,
        int commandTimeoutSeconds,
        string? tableName,
        CancellationToken cancellationToken) where T : class;

    ValueTask<int> BulkImportAsync<T>(
        DbConnection connection,
        DbTransaction? transaction,
        IAsyncEnumerable<T> items,
        LinqToDbBulkOptions options,
        int commandTimeoutSeconds,
        string? tableName,
        CancellationToken cancellationToken) where T : class;
}
