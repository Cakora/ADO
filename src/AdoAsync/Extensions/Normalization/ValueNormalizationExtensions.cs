using System;

using AdoAsync.Execution;

namespace AdoAsync.Extensions.Execution;

/// <summary>Normalization helpers to make post-fetch values consistent across providers.</summary>
public static class ValueNormalizationExtensions
{
    /// <summary>Normalize a raw value from a DataRow or IDataRecord based on the declared DbDataType.</summary>
    /// <remarks>
    /// Purpose:
    /// Convert provider-returned values into consistent CLR shapes based on declared <see cref="DbDataType"/>.
    ///
    /// When to use:
    /// - Values read from DataRow/IDataRecord/output parameters (post-fetch)
    /// - Cross-provider code that must be stable across SQL Server/PostgreSQL/Oracle
    ///
    /// When NOT to use:
    /// - Parameter binding (pre-execution)
    /// - When you require strict conversion failures (this is best-effort and may fall back to raw values)
    ///
    /// Lifetime / Ownership:
    /// - Source owner: caller owns <paramref name="value"/>.
    /// - Result owner: caller owns the returned normalized value.
    /// - Source disposal: not applicable.
    /// - Result release: release by dropping references (GC).
    /// </remarks>
    /// <param name="value">Value to normalize.</param>
    /// <param name="dataType">Declared cross-provider data type.</param>
    /// <returns>Normalized value (null for DBNull; bool/Guid normalized when possible).</returns>
    public static object? NormalizeByType(this object? value, DbDataType dataType)
    {
        return DbValueNormalizer.Normalize(value, dataType);
    }

    /// <summary>Normalize a raw value and return it as nullable when the type matches.</summary>
    /// <remarks>
    /// Purpose:
    /// Normalize a post-fetch value and return a typed nullable value if the normalized CLR type matches.
    ///
    /// When to use:
    /// - You expect a struct value but want a safe nullable return (no exceptions)
    ///
    /// When NOT to use:
    /// - You want strict type enforcement (use explicit conversion and throw on mismatch)
    ///
    /// Lifetime / Ownership:
    /// - Source owner: caller owns <paramref name="value"/>.
    /// - Result owner: caller owns the returned nullable value.
    /// - Source disposal: not applicable.
    /// - Result release: release by dropping references (GC).
    /// </remarks>
    /// <typeparam name="T">Target struct type.</typeparam>
    /// <param name="value">Raw value to normalize.</param>
    /// <param name="dataType">Declared cross-provider data type.</param>
    /// <returns>Nullable normalized value.</returns>
    public static T? NormalizeAsNullable<T>(this object? value, DbDataType dataType) where T : struct
    {
        var normalized = value.NormalizeByType(dataType);
        return normalized is T typed ? typed : (T?)null;
    }
}

