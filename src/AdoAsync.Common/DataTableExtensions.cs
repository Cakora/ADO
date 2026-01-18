using System;
using System.Collections.Generic;
using System.Data;

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
        foreach (DataColumn column in normalized.Columns)
        {
            if (columnTypes.TryGetValue(column.ColumnName, out var type))
            {
                column.DataType = type.IsEnum ? Enum.GetUnderlyingType(type) : type;
            }
        }

        foreach (DataRow row in table.Rows)
        {
            var newRow = normalized.NewRow();
            for (var i = 0; i < table.Columns.Count; i++)
            {
                var value = row[i];
                if (value is DBNull)
                {
                    newRow[i] = DBNull.Value;
                    continue;
                }

                var columnName = table.Columns[i].ColumnName;
                if (columnTypes.TryGetValue(columnName, out var targetType))
                {
                    var resolvedType = targetType.IsEnum ? Enum.GetUnderlyingType(targetType) : targetType;
                    newRow[i] = CommonValueConverter.ConvertValue(value, resolvedType);
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

}
