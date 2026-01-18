using System;

namespace AdoAsync;

/// <summary>
/// Result contract for bulk import operations.
/// </summary>
public sealed record BulkImportResult
{
    #region Members
    /// <summary>Indicates whether the operation succeeded.</summary>
    public required bool Success { get; init; }

    /// <summary>Total rows inserted.</summary>
    // Row count is returned to support caller-side auditing/verification.
    public int RowsInserted { get; init; }

    /// <summary>Elapsed duration for the import.</summary>
    // Duration is returned for diagnostics without requiring external timing.
    public TimeSpan Duration { get; init; }

    /// <summary>Error details when <see cref="Success"/> is false.</summary>
    // Error is returned instead of throwing to keep bulk import result explicit.
    public DbError? Error { get; init; }
    #endregion
}
