using System;
using LinqToDB;
using LinqToDB.Data;

namespace AdoAsync.BulkCopy.LinqToDb.Common;

/// <summary>
/// Configures linq2db bulk copy behavior. Defaults keep the feature disabled until explicitly enabled.
/// </summary>
public sealed record LinqToDbBulkOptions
{
    /// <summary>Enables the linq2db bulk copy path.</summary>
    public bool Enable { get; init; } = false;

    /// <summary>Bulk copy mode.</summary>
    public BulkCopyType BulkCopyType { get; init; } = BulkCopyType.Default;

    /// <summary>Optional timeout (seconds). Falls back to DbOptions.CommandTimeoutSeconds when not set.</summary>
    public int? BulkCopyTimeoutSeconds { get; init; }

    /// <summary>Batch size to send per round-trip.</summary>
    public int? MaxBatchSize { get; init; }

    /// <summary>Call progress callback after this many rows; 0 disables callbacks.</summary>
    public int? NotifyAfter { get; init; }

    /// <summary>Preserve identity values instead of letting the database generate them.</summary>
    public bool? KeepIdentity { get; init; }

    /// <summary>Enforce constraints during bulk copy (provider-specific).</summary>
    public bool? CheckConstraints { get; init; }

    /// <summary>Keep nulls instead of applying column defaults (provider-specific).</summary>
    public bool? KeepNulls { get; init; }

    /// <summary>Fire triggers during bulk copy when supported.</summary>
    public bool? FireTriggers { get; init; }

    /// <summary>Apply a table lock during bulk copy (provider-specific).</summary>
    public bool? TableLock { get; init; }

    /// <summary>Use an internal transaction when supported.</summary>
    public bool? UseInternalTransaction { get; init; }

    /// <summary>Use parameters for multi-row copy when supported.</summary>
    public bool? UseParameters { get; init; }

    /// <summary>Limit parameters per batch when parameters are used.</summary>
    public int? MaxParametersForBatch { get; init; }

    /// <summary>Degree of parallelism (provider-specific).</summary>
    public int? MaxDegreeOfParallelism { get; init; }

    /// <summary>Optional callback for progress reporting.</summary>
    public Action<BulkCopyRowsCopied>? OnRowsCopied { get; init; }
}
