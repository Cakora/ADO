using System;
using System.Collections.Generic;
using System.Data;

namespace AdoAsync;

/// <summary>
/// Result envelope for command execution. Supports scalar, materialized tables, output parameters, duration, retry count, and errors.
/// </summary>
public sealed record DbResult
{
    #region Members
    /// <summary>Indicates overall success.</summary>
    public bool Success { get; init; }

    /// <summary>Scalar value (if applicable).</summary>
    // Keeps a single result path for ExecuteScalar-like calls.
    public object? ScalarValue { get; init; }

    /// <summary>Materialized tables (allocation-heavy).</summary>
    public IReadOnlyList<DataTable>? Tables { get; init; }

    /// <summary>Output parameter values keyed by name.</summary>
    // Names are normalized (prefix trimmed) for cross-provider consistency.
    public IReadOnlyDictionary<string, object?>? OutputParameters { get; init; }

    /// <summary>Execution duration.</summary>
    public TimeSpan? ExecutionDuration { get; init; }

    /// <summary>Retry count applied during execution.</summary>
    public int RetryCount { get; init; }

    /// <summary>Error details when unsuccessful.</summary>
    public DbError? Error { get; init; }
    #endregion
}
