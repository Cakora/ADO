using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;

namespace AdoAsync.Extensions.Execution;

/// <summary>DataTable mapping helpers (buffered).</summary>
public static class DataTableExtensions
{
    /// <summary>
    /// Safely read and convert a column value from a <see cref="DataRow"/>.
    /// </summary>
    public static T? SafeGet<T>(this DataRow row, string column)
    {
        if (row is null) throw new ArgumentNullException(nameof(row));
        if (string.IsNullOrWhiteSpace(column)) throw new ArgumentException("Column name is required.", nameof(column));
        if (!row.Table.Columns.Contains(column))
        {
            throw new ArgumentException($"Column '{column}' does not exist.", nameof(column));
        }

        var target = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);
        var value = row[column];
        if (value is null || value is DBNull)
        {
            if (target.IsValueType && Nullable.GetUnderlyingType(typeof(T)) is null)
            {
                throw new InvalidOperationException($"Column '{column}' is null.");
            }

            return (T?)(object?)null;
        }

        if (value is T typed)
        {
            return typed;
        }
        if (target == typeof(char))
        {
            var s = value.ToString();
            return (T)(object)(string.IsNullOrEmpty(s) ? '\0' : s[0]);
        }

        if (target == typeof(bool))
        {
            if (value is bool b) return (T)(object)b;
            if (value is string bs)
            {
                if (bool.TryParse(bs, out var parsed)) return (T)(object)parsed;
                if (int.TryParse(bs, out var bi)) return (T)(object)(bi != 0);
            }

            if (value is IConvertible)
            {
                return (T)(object)(Convert.ToInt64(value) != 0);
            }
        }

        if (target == typeof(Guid))
        {
            if (value is Guid g) return (T)(object)g;
            if (value is string gs) return (T)(object)Guid.Parse(gs);
            if (value is byte[] gb) return (T)(object)new Guid(gb);
        }

        if (target == typeof(DateTimeOffset))
        {
            if (value is DateTimeOffset dto) return (T)(object)dto;
            if (value is DateTime dt)
            {
                if (dt.Kind == DateTimeKind.Utc)
                {
                    return (T)(object)new DateTimeOffset(dt, TimeSpan.Zero);
                }

                if (dt.Kind == DateTimeKind.Local)
                {
                    return (T)(object)new DateTimeOffset(dt);
                }

                var utc = DateTime.SpecifyKind(dt, DateTimeKind.Utc);
                return (T)(object)new DateTimeOffset(utc, TimeSpan.Zero);
            }
            if (value is string dts) return (T)(object)DateTimeOffset.Parse(dts, CultureInfo.InvariantCulture);
        }

        if (target.IsEnum)
        {
            if (value is string es) return (T)Enum.Parse(target, es, ignoreCase: true);
            var enumValue = Convert.ChangeType(value, Enum.GetUnderlyingType(target));
            return (T)Enum.ToObject(target, enumValue!);
        }

        return (T)Convert.ChangeType(value, target);
    }

    /// <summary>
    /// Project DataRow items to a list using the provided mapper.
    /// </summary>
    /// <remarks>
    /// Purpose:
    /// Convert a buffered <see cref="DataTable"/> into a <see cref="List{T}"/> using a fast indexed loop.
    ///
    /// When to use:
    /// - Oracle / RefCursor / buffered paths where streaming is not possible
    /// - When you need repeated filtering/grouping after materialization
    ///
    /// When NOT to use:
    /// - Large datasets (high memory)
    /// - SQL Server / PostgreSQL when streaming is available (prefer streaming)
    ///
    /// Lifetime / Ownership:
    /// - Source owner: caller owns <paramref name="table"/>.
    /// - Result owner: caller owns the returned <see cref="List{T}"/>.
    /// - Source disposal: dispose/release <paramref name="table"/> as soon as mapping is complete.
    /// - Result release: release by dropping references to the returned list (GC).
    /// </remarks>
    public static List<T> ToList<T>(this DataTable table, Func<DataRow, T> map)
    {
        if (table is null) throw new ArgumentNullException(nameof(table));
        if (map is null) throw new ArgumentNullException(nameof(map));

        var results = new List<T>(table.Rows.Count);
        for (var i = 0; i < table.Rows.Count; i++)
        {
            results.Add(map(table.Rows[i]));
        }

        return results;
    }

    /// <summary>
    /// Project DataRow items to an array using the provided mapper.
    /// </summary>
    /// <remarks>
    /// Purpose:
    /// Convert a buffered <see cref="DataTable"/> into a compact array representation in one pass.
    ///
    /// When to use:
    /// - Oracle / RefCursor / buffered paths where streaming is not possible
    /// - When you want predictable memory and fast iteration (array-backed)
    ///
    /// When NOT to use:
    /// - When you need incremental growth (prefer <see cref="ToList{T}"/>)
    /// - SQL Server / PostgreSQL when streaming is available (prefer streaming)
    ///
    /// Lifetime / Ownership:
    /// - Source owner: caller owns <paramref name="table"/>.
    /// - Result owner: caller owns the returned array.
    /// - Source disposal: dispose/release <paramref name="table"/> as soon as mapping is complete.
    /// - Result release: release by dropping references to the returned array (GC).
    /// </remarks>
    public static T[] ToArray<T>(this DataTable table, Func<DataRow, T> map)
    {
        if (table is null) throw new ArgumentNullException(nameof(table));
        if (map is null) throw new ArgumentNullException(nameof(map));

        var rows = table.Rows;
        var results = new T[rows.Count];
        for (var i = 0; i < rows.Count; i++)
        {
            results[i] = map(rows[i]);
        }

        return results;
    }
}
