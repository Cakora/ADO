using System.Collections.Generic;
using System.Data;

namespace AdoAsync;

/// <summary>
/// Structured multi-result containing buffered result tables and optional output parameters.
/// </summary>
public sealed class MultiResult
{
    /// <summary>Buffered result sets in the order returned by the provider.</summary>
    public IReadOnlyList<DataTable> Tables { get; init; } = System.Array.Empty<DataTable>();

    /// <summary>Output parameters captured during execution (keyed by parameter name).</summary>
    public IReadOnlyDictionary<string, object?>? OutputParameters { get; init; }
}
