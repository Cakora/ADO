using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Reflection;
using System.Text;

namespace AdoAsync.Common;

public static class CsvExtensions
{
    private static readonly ConcurrentDictionary<Type, PropertyInfo[]> PropertyCache = new();

    public static string ToCsv(this DataTable table, IReadOnlyDictionary<string, string?>? columnMap = null, bool includeHeader = true)
    {
        var builder = new StringBuilder();
        AppendCsv(table, builder, columnMap, includeHeader);
        return builder.ToString();
    }

    public static string ToCsv<T>(
        this IEnumerable<T> items,
        IReadOnlyList<CsvColumn<T>>? columns = null,
        IReadOnlyDictionary<string, string?>? columnMap = null,
        bool includeHeader = true)
    {
        var builder = new StringBuilder();
        AppendCsv(items, builder, columns, columnMap, includeHeader);
        return builder.ToString();
    }

    public static void AppendCsv(this DataTable table, StringBuilder builder, IReadOnlyDictionary<string, string?>? columnMap = null, bool includeHeader = true)
    {
        if (table is null)
        {
            throw new ArgumentNullException(nameof(table));
        }

        if (builder is null)
        {
            throw new ArgumentNullException(nameof(builder));
        }

        var hasHeader = builder.Length > 0;
        if (includeHeader && !hasHeader)
        {
            for (var i = 0; i < table.Columns.Count; i++)
            {
                if (i > 0)
                {
                    builder.Append(',');
                }

                var column = table.Columns[i].ColumnName;
                var name = ResolveName(columnMap, column);
                // Write header column name (mapped if provided).
                AppendEscaped(builder, name);
            }

            builder.AppendLine();
        }

        for (var rowIndex = 0; rowIndex < table.Rows.Count; rowIndex++)
        {
            var row = table.Rows[rowIndex];
            for (var i = 0; i < table.Columns.Count; i++)
            {
                if (i > 0)
                {
                    builder.Append(',');
                }

                var value = row[i];
                AppendEscaped(builder, value is DBNull ? null : value);
            }

            builder.AppendLine();
        }
    }

    public static void AppendCsv<T>(
        this IEnumerable<T> items,
        StringBuilder builder,
        IReadOnlyList<CsvColumn<T>>? columns = null,
        IReadOnlyDictionary<string, string?>? columnMap = null,
        bool includeHeader = true)
    {
        if (items is null)
        {
            throw new ArgumentNullException(nameof(items));
        }

        if (builder is null)
        {
            throw new ArgumentNullException(nameof(builder));
        }

        var resolvedColumns = columns ?? BuildColumns<T>();
        var hasHeader = builder.Length > 0;

        if (includeHeader && !hasHeader)
        {
            for (var i = 0; i < resolvedColumns.Count; i++)
            {
                if (i > 0)
                {
                    builder.Append(',');
                }

                var name = ResolveName(columnMap, resolvedColumns[i].Name);
                AppendEscaped(builder, name);
            }

            builder.AppendLine();
        }

        foreach (var item in items)
        {
            for (var i = 0; i < resolvedColumns.Count; i++)
            {
                if (i > 0)
                {
                    builder.Append(',');
                }

                var value = resolvedColumns[i].Selector(item);
                // Write each projected column value.
                AppendEscaped(builder, value);
            }

            builder.AppendLine();
        }
    }

    private static IReadOnlyList<CsvColumn<T>> BuildColumns<T>()
    {
        var type = typeof(T);
        var props = PropertyCache.GetOrAdd(type, t => t.GetProperties(BindingFlags.Instance | BindingFlags.Public));
        var columns = new CsvColumn<T>[props.Length];
        for (var i = 0; i < props.Length; i++)
        {
            var property = props[i];
            columns[i] = new CsvColumn<T>(property.Name, item => property.GetValue(item));
        }

        return columns;
    }

    private static string ResolveName(IReadOnlyDictionary<string, string?>? map, string name)
    {
        if (map is null)
        {
            return name;
        }

        if (map.TryGetValue(name, out var mapped) && !string.IsNullOrWhiteSpace(mapped))
        {
            return mapped;
        }

        return name;
    }

    private static void AppendEscaped(StringBuilder builder, object? value)
    {
        if (value is null)
        {
            return;
        }

        var text = Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
        var needsQuotes = text.IndexOfAny(new[] { ',', '"', '\r', '\n' }) >= 0;

        if (!needsQuotes)
        {
            builder.Append(text);
            return;
        }

        // Escape quotes per CSV by doubling them.
        builder.Append('"');
        foreach (var ch in text)
        {
            if (ch == '"')
            {
                builder.Append("\"\"");
            }
            else
            {
                builder.Append(ch);
            }
        }
        builder.Append('"');
    }
}

public readonly record struct CsvColumn<T>(string Name, Func<T, object?> Selector);
