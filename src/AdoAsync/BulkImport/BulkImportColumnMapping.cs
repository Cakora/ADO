namespace AdoAsync;

/// <summary>
/// Maps a source column to a destination column for bulk import.
/// </summary>
public sealed record BulkImportColumnMapping
{
    #region Members
    /// <summary>Source column name in the reader.</summary>
    // Explicit naming keeps mappings stable even if the reader column order changes.
    public required string SourceColumn { get; init; }

    /// <summary>Destination column name in the target table.</summary>
    // Explicit mappings avoid relying on column order.
    public required string DestinationColumn { get; init; }
    #endregion
}
