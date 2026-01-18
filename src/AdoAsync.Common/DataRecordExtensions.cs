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

        if (record.IsDBNull(ordinal))
        {
            return default;
        }

        var value = record.GetValue(ordinal);
        if (value is T typed)
        {
            return typed;
        }

        return (T)CommonValueConverter.ConvertValue(value, typeof(T));
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
