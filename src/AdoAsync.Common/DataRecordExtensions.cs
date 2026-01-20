using System;
using System.Data;

namespace AdoAsync.Common;

public static class DataRecordExtensions
{
    public static T? Get<T>(this IDataRecord record, int ordinal)
    {
        if (record is null)
        {
            throw new ArgumentNullException(nameof(record));
        }

        // Return default for DBNull/null to avoid conversions on missing values.
        if (record.IsDBNull(ordinal))
        {
            return default;
        }

        var value = record.GetValue(ordinal);
        if (value is null)
        {
            return default;
        }

        if (value is T typed)
        {
            return typed;
        }

        // Normalize provider-specific scalars to the requested type.
        var converted = CommonValueConverter.ConvertValue(value, typeof(T));
        if (converted is null)
        {
            return default;
        }

        return (T)converted;
    }

    public static T? Get<T>(this IDataRecord record, string name)
    {
        if (record is null)
        {
            throw new ArgumentNullException(nameof(record));
        }

        var ordinal = record.GetOrdinal(name);
        return record.Get<T>(ordinal);
    }

}
