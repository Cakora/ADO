using System;
using LinqToDB.Data;

namespace AdoAsync.BulkCopy.LinqToDb.Common;

internal static class BulkCopyOptionsMapper
{
    internal static BulkCopyOptions Map(LinqToDbBulkOptions options, string? tableName, int commandTimeoutSeconds)
    {
        if (options is null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        return new BulkCopyOptions(
            MaxBatchSize: options.MaxBatchSize,
            BulkCopyTimeout: options.BulkCopyTimeoutSeconds ?? commandTimeoutSeconds,
            BulkCopyType: options.BulkCopyType,
            CheckConstraints: options.CheckConstraints,
            KeepIdentity: options.KeepIdentity,
            TableLock: options.TableLock,
            KeepNulls: options.KeepNulls,
            FireTriggers: options.FireTriggers,
            UseInternalTransaction: options.UseInternalTransaction,
            TableName: tableName,
            NotifyAfter: options.NotifyAfter ?? 0,
            RowsCopiedCallback: options.OnRowsCopied,
            UseParameters: options.UseParameters ?? false,
            MaxParametersForBatch: options.MaxParametersForBatch,
            MaxDegreeOfParallelism: options.MaxDegreeOfParallelism)
        {
            // Keep WithoutSession as default; not applicable for current providers.
        };
    }
}
