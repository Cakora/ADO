using System;
using System.Globalization;

namespace AdoAsync.Common;

internal static class CommonValueConverter
{
    private static readonly CultureInfo InvariantCulture = CultureInfo.InvariantCulture;

    internal static object? ConvertValue(object? value, Type targetType)
    {
        if (targetType is null)
        {
            throw new ArgumentNullException(nameof(targetType));
        }

        var nullableUnderlying = Nullable.GetUnderlyingType(targetType);
        var resolvedTargetType = nullableUnderlying ?? targetType;

        // Fast path: return unchanged when already compatible.
        if (value is not null && resolvedTargetType.IsInstanceOfType(value))
        {
            return value;
        }

        if (resolvedTargetType == typeof(object))
        {
            return value;
        }

        if (value is null)
        {
            if (!resolvedTargetType.IsValueType || nullableUnderlying is not null)
            {
                return null;
            }

            throw new InvalidCastException($"Cannot convert null to non-nullable type '{resolvedTargetType}'.");
        }

        if (resolvedTargetType == typeof(Guid))
        {
            // Handle Guid as native, byte[16], or string.
            if (value is Guid guid)
            {
                return guid;
            }

            if (value is byte[] bytes && bytes.Length == 16)
            {
                return new Guid(bytes);
            }

            var text = Convert.ToString(value, InvariantCulture);
            return string.IsNullOrWhiteSpace(text) ? Guid.Empty : Guid.Parse(text);
        }

        if (resolvedTargetType == typeof(DateTimeOffset))
        {
            // Normalize DateTime -> DateTimeOffset while preserving kind.
            return value switch
            {
                DateTimeOffset offset => offset,
                DateTime dateTime => new DateTimeOffset(dateTime.Kind == DateTimeKind.Unspecified
                    ? DateTime.SpecifyKind(dateTime, DateTimeKind.Utc)
                    : dateTime),
                string text when DateTimeOffset.TryParse(text, InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed) => parsed,
                string => throw new InvalidCastException($"Cannot convert '{value}' to '{resolvedTargetType}'."),
                _ => throw new InvalidCastException($"Cannot convert '{value.GetType()}' to '{resolvedTargetType}'.")
            };
        }

        if (resolvedTargetType == typeof(DateTime))
        {
            return value switch
            {
                DateTime dateTime => dateTime,
                _ => Convert.ChangeType(value, resolvedTargetType, InvariantCulture)
            };
        }

        if (resolvedTargetType == typeof(TimeSpan))
        {
            // Accept TimeSpan or DateTime (using its time component).
            return value switch
            {
                TimeSpan span => span,
                DateTime dateValue => dateValue.TimeOfDay,
                string text when TimeSpan.TryParse(text, InvariantCulture, out var parsed) => parsed,
                string => throw new InvalidCastException($"Cannot convert '{value}' to '{resolvedTargetType}'."),
                _ => throw new InvalidCastException($"Cannot convert '{value.GetType()}' to '{resolvedTargetType}'.")
            };
        }

        if (resolvedTargetType.IsEnum)
        {
            // Parse enums from string or numeric underlying value.
            if (value is string enumText)
            {
                return Enum.Parse(resolvedTargetType, enumText, ignoreCase: true);
            }

            var enumUnderlying = Enum.GetUnderlyingType(resolvedTargetType);
            var normalizedValue = value.GetType() == enumUnderlying
                ? value
                : Convert.ChangeType(value, enumUnderlying, InvariantCulture);
            return Enum.ToObject(resolvedTargetType, normalizedValue);
        }

        return Convert.ChangeType(value, resolvedTargetType, InvariantCulture);
    }
}
