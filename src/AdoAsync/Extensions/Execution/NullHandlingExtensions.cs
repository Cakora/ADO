using System;

namespace AdoAsync.Extensions.Execution;

internal static class NullHandlingExtensions
{
    /// <summary>Return null when the value is DBNull to simplify mapping.</summary>
    /// <param name="value">Value to normalize.</param>
    /// <returns>Null when value is DBNull; otherwise the original value.</returns>
    public static object? ToNullIfDbNull(this object? value) => value is DBNull ? null : value;
}
