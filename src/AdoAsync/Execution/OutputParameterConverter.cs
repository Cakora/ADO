using System;
using System.Globalization;

namespace AdoAsync.Execution;

internal static class OutputParameterConverter
{
    internal static object? Normalize(object? value, DbDataType dataType)
    {
        if (value is null or DBNull)
        {
            return null;
        }

        // Best-effort normalization for cross-provider consistency; falls back to raw value.
        // This intentionally avoids throwing so output parameters remain usable.
        try
        {
            return dataType switch
            {
                DbDataType.String
                    or DbDataType.AnsiString
                    or DbDataType.StringFixed
                    or DbDataType.AnsiStringFixed
                    or DbDataType.Clob
                    or DbDataType.NClob
                    or DbDataType.Json
                    or DbDataType.Xml => Convert.ToString(value, CultureInfo.InvariantCulture),
                DbDataType.Int16 => Convert.ToInt16(value, CultureInfo.InvariantCulture),
                DbDataType.Int32 => Convert.ToInt32(value, CultureInfo.InvariantCulture),
                DbDataType.Int64 => Convert.ToInt64(value, CultureInfo.InvariantCulture),
                DbDataType.Byte => Convert.ToByte(value, CultureInfo.InvariantCulture),
                DbDataType.SByte => Convert.ToSByte(value, CultureInfo.InvariantCulture),
                DbDataType.UInt16 => Convert.ToUInt16(value, CultureInfo.InvariantCulture),
                DbDataType.UInt32 => Convert.ToUInt32(value, CultureInfo.InvariantCulture),
                DbDataType.UInt64 => Convert.ToUInt64(value, CultureInfo.InvariantCulture),
                DbDataType.Decimal or DbDataType.Currency => Convert.ToDecimal(value, CultureInfo.InvariantCulture),
                DbDataType.Double => Convert.ToDouble(value, CultureInfo.InvariantCulture),
                DbDataType.Single => Convert.ToSingle(value, CultureInfo.InvariantCulture),
                DbDataType.Boolean => Convert.ToBoolean(value, CultureInfo.InvariantCulture),
                DbDataType.Guid => NormalizeGuid(value),
                DbDataType.Binary or DbDataType.Blob => NormalizeBinary(value),
                DbDataType.Date
                    or DbDataType.DateTime
                    or DbDataType.DateTime2 => Convert.ToDateTime(value, CultureInfo.InvariantCulture),
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
