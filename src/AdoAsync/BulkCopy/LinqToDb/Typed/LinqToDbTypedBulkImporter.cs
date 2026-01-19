using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using AdoAsync.BulkCopy.LinqToDb.Common;
using LinqToDB;
using LinqToDB.Data;

namespace AdoAsync.BulkCopy.LinqToDb.Typed;

internal sealed class LinqToDbTypedBulkImporter : ILinqToDbTypedBulkImporter
{
    private readonly LinqToDbConnectionFactory _connectionFactory;

    public LinqToDbTypedBulkImporter(LinqToDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    public async ValueTask<int> BulkImportAsync<T>(
        DbConnection connection,
        DbTransaction? transaction,
        IEnumerable<T> items,
        LinqToDbBulkOptions options,
        int commandTimeoutSeconds,
        string? tableName,
        CancellationToken cancellationToken) where T : class
    {
        Validate.Required(connection, nameof(connection));
        Validate.Required(items, nameof(items));
        Validate.Required(options, nameof(options));

        await using var dataConnection = _connectionFactory.Create(connection, transaction);
        var resolvedTableName = _connectionFactory.NormalizeTableName(tableName);
        var bulkOptions = BulkCopyOptionsMapper.Map(options, resolvedTableName, commandTimeoutSeconds);
        var result = await dataConnection.BulkCopyAsync(bulkOptions, items, cancellationToken).ConfigureAwait(false);
        return (int)result.RowsCopied;
    }

    public async ValueTask<int> BulkImportAsync<T>(
        DbConnection connection,
        DbTransaction? transaction,
        IAsyncEnumerable<T> items,
        LinqToDbBulkOptions options,
        int commandTimeoutSeconds,
        string? tableName,
        CancellationToken cancellationToken) where T : class
    {
        Validate.Required(connection, nameof(connection));
        Validate.Required(items, nameof(items));
        Validate.Required(options, nameof(options));

        await using var dataConnection = _connectionFactory.Create(connection, transaction);
        var resolvedTableName = _connectionFactory.NormalizeTableName(tableName);
        var bulkOptions = BulkCopyOptionsMapper.Map(options, resolvedTableName, commandTimeoutSeconds);
        var result = await dataConnection.BulkCopyAsync(bulkOptions, items, cancellationToken).ConfigureAwait(false);
        return (int)result.RowsCopied;
    }
}
