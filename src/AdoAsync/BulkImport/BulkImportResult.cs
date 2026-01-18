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
    public int RowsInserted { get; init; }

    /// <summary>Elapsed duration for the import.</summary>
    public TimeSpan Duration { get; init; }

    /// <summary>Error details when <see cref="Success"/> is false.</summary>
    public DbError? Error { get; init; }
    #endregion
}
