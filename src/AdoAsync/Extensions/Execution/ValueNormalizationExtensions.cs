using System;
using System.Data;

namespace AdoAsync.Extensions.Execution;

/// <summary>Normalization helpers to make DataRow/DataReader values LINQ-friendly across providers.</summary>
internal static class ValueNormalizationExtensions
{
    /// <summary>Normalize a raw value from a DataRow or IDataRecord based on the declared DbDataType.</summary>
    /// <param name="value">Value to normalize.</param>
    /// <param name="dataType">Declared cross-provider data type.</param>
    /// <returns>Normalized value (null for DBNull; bool/Guid normalized when possible).</returns>
    public static object? NormalizeByType(this object? value, DbDataType dataType)
    {
        if (value is DBNull)
        {
            return null;
        }

        return dataType switch
        {
            DbDataType.Boolean => value is null ? null : NormalizeBoolean(value),
            DbDataType.Guid => value is null ? null : NormalizeGuid(value),
            DbDataType.Int64 => value is null ? null : NormalizeInt64(value),
            DbDataType.UInt64 => value is null ? null : NormalizeUInt64(value),
            DbDataType.Decimal => value is null ? null : NormalizeDecimal(value),
            DbDataType.DateTimeOffset => value is null ? null : NormalizeDateTimeOffset(value),
            _ => value
        };
    }

    private static object NormalizeBoolean(object value) =>
        value switch
        {
            bool b => b,
            byte by => by switch
            {
                0 => false,
                1 => true,
                _ => by
            },
            short s => s switch
            {
                0 => false,
                1 => true,
                _ => s
            },
            decimal d when d == 0m || d == 1m => d == 1m,
            _ => value
        };

    private static object NormalizeGuid(object value) =>
        value switch
        {
            Guid g => g,
            byte[] bytes when bytes.Length == 16 => new Guid(bytes),
            string s when Guid.TryParse(s, out var parsed) => parsed,
            _ => value
        };

    private static object NormalizeInt64(object value) =>
        value switch
        {
            long l => l,
            decimal d when TryDecimalToInt64(d, out var l) => l,
            int i => (long)i,
            short s => (long)s,
            _ => value
        };

    private static object NormalizeUInt64(object value) =>
        value switch
        {
            ulong ul => ul,
            decimal d when TryDecimalToUInt64(d, out var ul) => ul,
            long l when l >= 0 => (ulong)l,
            _ => value
        };

    private static object NormalizeDecimal(object value) =>
        value switch
        {
            decimal d => d,
            double db => (decimal)db,
            float f => (decimal)f,
            long l => l,
            int i => i,
            short s => s,
            byte b => b,
            _ => value
        };

    private static object NormalizeDateTimeOffset(object value) =>
        value switch
        {
            DateTimeOffset dto => dto,
            DateTime dt => new DateTimeOffset(dt),
            _ => value
        };

    /// <summary>Normalize a raw value and return it as nullable when the type matches.</summary>
    /// <typeparam name="T">Target struct type.</typeparam>
    /// <param name="value">Raw value to normalize.</param>
    /// <param name="dataType">Declared cross-provider data type.</param>
    /// <returns>Nullable normalized value.</returns>
    public static T? NormalizeAsNullable<T>(this object? value, DbDataType dataType) where T : struct
    {
        var normalized = value.NormalizeByType(dataType);
        return normalized is T typed ? typed : (T?)null;
    }

    private static bool TryDecimalToInt64(decimal value, out long result)
    {
        result = 0;
        if (decimal.Truncate(value) != value)
        {
            return false;
        }

        if (value < long.MinValue || value > long.MaxValue)
        {
            return false;
        }

        result = (long)value;
        return true;
    }

    private static bool TryDecimalToUInt64(decimal value, out ulong result)
    {
        result = 0;
        if (decimal.Truncate(value) != value || value < 0)
        {
            return false;
        }

        if (value > ulong.MaxValue)
        {
            return false;
        }

        result = (ulong)value;
        return true;
    }
}
