using System;
using System.Globalization;

namespace AdoAsync.Execution
{
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
}

namespace AdoAsync.Extensions.Execution
{
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
            return AdoAsync.Execution.DbValueNormalizer.Normalize(value, dataType);
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
}
