using System;
using System.Globalization;

namespace AdoAsync.Common;

internal static class CommonValueConverter
{
    internal static object ConvertValue(object value, Type targetType)
    {
        if (targetType == typeof(Guid))
        {
            if (value is Guid guid)
            {
                return guid;
            }

            if (value is byte[] bytes && bytes.Length == 16)
            {
                return new Guid(bytes);
            }

            var text = Convert.ToString(value, CultureInfo.InvariantCulture);
            return string.IsNullOrWhiteSpace(text) ? Guid.Empty : Guid.Parse(text);
        }

        if (targetType == typeof(DateTimeOffset))
        {
            return value switch
            {
                DateTimeOffset offset => offset,
                DateTime dateTime => new DateTimeOffset(dateTime.Kind == DateTimeKind.Unspecified
                    ? DateTime.SpecifyKind(dateTime, DateTimeKind.Utc)
                    : dateTime),
                _ => Convert.ChangeType(value, targetType, CultureInfo.InvariantCulture)
            };
        }

        if (targetType == typeof(DateTime))
        {
            return value switch
            {
                DateTime dateTime => dateTime,
                _ => Convert.ChangeType(value, targetType, CultureInfo.InvariantCulture)
            };
        }

        if (targetType == typeof(TimeSpan))
        {
            return value switch
            {
                TimeSpan span => span,
                DateTime dateValue => dateValue.TimeOfDay,
                _ => Convert.ChangeType(value, targetType, CultureInfo.InvariantCulture)
            };
        }

        if (targetType.IsEnum)
        {
            return Enum.ToObject(targetType, value);
        }

        return Convert.ChangeType(value, targetType, CultureInfo.InvariantCulture);
    }
}
