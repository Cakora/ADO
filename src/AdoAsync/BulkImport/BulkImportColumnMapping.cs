namespace AdoAsync;

/// <summary>
/// Maps a source column to a destination column for bulk import.
/// </summary>
public sealed record BulkImportColumnMapping
{
    #region Members
    /// <summary>Source column name in the reader.</summary>
    public required string SourceColumn { get; init; }

    /// <summary>Destination column name in the target table.</summary>
    // Explicit mapping avoids reliance on positional ordering.
    public required string DestinationColumn { get; init; }
    #endregion
}
