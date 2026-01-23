using System;

namespace AdoAsync.Extensions.Execution;

/// <summary>Helpers for normalizing null/DBNull values during mapping.</summary>
public static class NullHandlingExtensions
{
    /// <summary>
    /// Return null when the value is <see cref="DBNull"/> to simplify mapping.
    /// </summary>
    /// <remarks>
    /// Purpose:
    /// Normalize provider-returned <see cref="DBNull"/> into <see langword="null"/> for post-fetch mapping.
    ///
    /// When to use:
    /// - Values from DataRow/IDataRecord/output parameters (post-fetch)
    ///
    /// When NOT to use:
    /// - Parameter binding (pre-execution)
    /// - When you want to preserve DBNull as a sentinel value
    ///
    /// Lifetime / Ownership:
    /// - Source owner: caller owns <paramref name="value"/>.
    /// - Result owner: caller owns the returned normalized value.
    /// - Source disposal: not applicable.
    /// - Result release: release by dropping references (GC).
    /// </remarks>
    public static object? ToNullIfDbNull(this object? value) => value is DBNull ? null : value;
}
