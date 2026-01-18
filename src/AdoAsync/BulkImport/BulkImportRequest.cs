using System.Collections.Generic;
using System.Data.Common;

namespace AdoAsync;

/// <summary>
/// Provider-agnostic bulk import request.
/// </summary>
public sealed record BulkImportRequest
{
    #region Members
    /// <summary>Destination table name (must be allow-listed).</summary>
    public required string DestinationTable { get; init; }

    /// <summary>Source reader supplying rows.</summary>
    // DbDataReader keeps bulk import streaming and avoids buffering.
    public required DbDataReader SourceReader { get; init; }

    /// <summary>Column mappings (source → destination).</summary>
    public required IReadOnlyList<BulkImportColumnMapping> ColumnMappings { get; init; }

    /// <summary>Optional batch size for providers that support it.</summary>
    public int? BatchSize { get; init; }

    /// <summary>Allow-list of destination tables (required for validation).</summary>
    public IReadOnlySet<string>? AllowedDestinationTables { get; init; }

    /// <summary>Allow-list of destination columns (required when identifiers are supplied).</summary>
    public IReadOnlySet<string>? AllowedDestinationColumns { get; init; }
    #endregion
}
