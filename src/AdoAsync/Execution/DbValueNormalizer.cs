using System;
using System.Globalization;

namespace AdoAsync.Execution;

/// <summary>Best-effort normalization for provider-returned scalar values based on declared <see cref="DbDataType"/>.</summary>
internal static class DbValueNormalizer
{
    internal static object? Normalize(object? value, DbDataType dataType)
    {
        if (value is null or DBNull)
        {
            return null;
        }

        // Best-effort normalization for cross-provider consistency; falls back to raw value.
        // This intentionally avoids throwing so values remain usable.
        try
        {
            var inv = CultureInfo.InvariantCulture;
            return dataType switch
            {
                DbDataType.String
                    or DbDataType.AnsiString
                    or DbDataType.StringFixed
                    or DbDataType.AnsiStringFixed
                    or DbDataType.Clob
                    or DbDataType.NClob
                    or DbDataType.Json
                    or DbDataType.Xml => Convert.ToString(value, inv),
                DbDataType.Int16 => Convert.ToInt16(value, inv),
                DbDataType.Int32 => Convert.ToInt32(value, inv),
                DbDataType.Int64 => Convert.ToInt64(value, inv),
                DbDataType.Byte => Convert.ToByte(value, inv),
                DbDataType.SByte => Convert.ToSByte(value, inv),
                DbDataType.UInt16 => Convert.ToUInt16(value, inv),
                DbDataType.UInt32 => Convert.ToUInt32(value, inv),
                DbDataType.UInt64 => Convert.ToUInt64(value, inv),
                DbDataType.Decimal or DbDataType.Currency => Convert.ToDecimal(value, inv),
                DbDataType.Double => Convert.ToDouble(value, inv),
                DbDataType.Single => Convert.ToSingle(value, inv),
                DbDataType.Boolean => Convert.ToBoolean(value, inv),
                DbDataType.Guid => NormalizeGuid(value),
                DbDataType.Binary or DbDataType.Blob or DbDataType.Timestamp => NormalizeBinary(value),
                DbDataType.Date
                    or DbDataType.DateTime
                    or DbDataType.DateTime2 => Convert.ToDateTime(value, inv),
                DbDataType.DateTimeOffset => NormalizeDateTimeOffset(value),
                DbDataType.Time or DbDataType.Interval => NormalizeTimeSpan(value),
                _ => value
            };
        }
        catch
        {
            return value;
        }
    }

    private static object NormalizeGuid(object value)
    {
        if (value is Guid guid)
        {
            return guid;
        }

        if (value is byte[] bytes && bytes.Length == 16)
        {
            return new Guid(bytes);
        }

        if (value is string text && Guid.TryParse(text, out var parsed))
        {
            return parsed;
        }

        return value;
    }

    private static object NormalizeBinary(object value)
    {
        if (value is byte[] bytes)
        {
            return bytes;
        }

        return value;
    }

    private static object NormalizeDateTimeOffset(object value)
    {
        if (value is DateTimeOffset offset)
        {
            return offset;
        }

        if (value is DateTime dateTime)
        {
            return new DateTimeOffset(dateTime);
        }

        var parsed = Convert.ToDateTime(value, CultureInfo.InvariantCulture);
        return new DateTimeOffset(parsed);
    }

    private static object NormalizeTimeSpan(object value)
    {
        if (value is TimeSpan span)
        {
            return span;
        }

        if (value is DateTime dateTime)
        {
            return dateTime.TimeOfDay;
        }

        if (value is string text && TimeSpan.TryParse(text, CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }

        return value;
    }
}

