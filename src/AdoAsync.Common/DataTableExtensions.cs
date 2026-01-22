using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;

namespace AdoAsync.Common;

public static class DataTableExtensions
{
    public static DataTable Normalize(
        this DataTable table,
        IReadOnlyDictionary<string, Type> columnTypes)
    {
        if (table is null)
        {
            throw new ArgumentNullException(nameof(table));
        }

        if (columnTypes is null)
        {
            throw new ArgumentNullException(nameof(columnTypes));
        }

        var normalized = table.Clone();
        EnsureGenericColumnNames(normalized);
        foreach (DataColumn column in normalized.Columns)
        {
            if (columnTypes.TryGetValue(column.ColumnName, out var type))
            {
                var nullableUnderlying = Nullable.GetUnderlyingType(type);
                var resolvedType = nullableUnderlying ?? (type.IsEnum ? Enum.GetUnderlyingType(type) : type);
                column.DataType = resolvedType;
                if (nullableUnderlying is not null)
                {
                    column.AllowDBNull = true;
                }
            }
        }

        for (var rowIndex = 0; rowIndex < table.Rows.Count; rowIndex++)
        {
            var row = table.Rows[rowIndex];
            var newRow = normalized.NewRow();
            for (var i = 0; i < table.Columns.Count; i++)
            {
                var value = row[i];
                if (value is null || value is DBNull)
                {
                    newRow[i] = DBNull.Value;
                    continue;
                }

                var columnName = table.Columns[i].ColumnName;
                if (columnTypes.TryGetValue(columnName, out var targetType))
                {
                    var nullableUnderlying = Nullable.GetUnderlyingType(targetType);
                    var resolvedType = nullableUnderlying ?? (targetType.IsEnum ? Enum.GetUnderlyingType(targetType) : targetType);
                    var conversionType = nullableUnderlying ?? targetType;
                    var converted = CommonValueConverter.ConvertValue(value, conversionType);

                    if (conversionType.IsEnum && resolvedType != conversionType)
                    {
                        converted = Convert.ChangeType(converted, resolvedType, CultureInfo.InvariantCulture);
                    }

                    newRow[i] = converted;
                }
                else
                {
                    newRow[i] = value;
                }
            }
            normalized.Rows.Add(newRow);
        }

        return normalized;
    }

    private static void EnsureGenericColumnNames(DataTable table)
    {
        var comparer = table.CaseSensitive ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase;
        var usedNames = new HashSet<string>(comparer);

        for (var i = 0; i < table.Columns.Count; i++)
        {
            var column = table.Columns[i];
            if (string.IsNullOrWhiteSpace(column.ColumnName))
            {
                column.ColumnName = $"Column{i + 1}";
            }

            if (usedNames.Add(column.ColumnName))
            {
                continue;
            }

            var suffix = 2;
            while (!usedNames.Add($"{column.ColumnName}_{suffix}"))
            {
                suffix++;
            }

            column.ColumnName = $"{column.ColumnName}_{suffix}";
        }
    }

}
