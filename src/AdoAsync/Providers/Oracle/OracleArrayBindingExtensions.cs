using System;
using System.Collections.Generic;
using System.Data;

namespace AdoAsync.Providers.Oracle;

/// <summary>
/// Oracle parameter helpers (PL/SQL associative array binding).
/// </summary>
public static class OracleArrayBindingExtensions
{
    /// <summary>
    /// Creates a single Oracle array-binding parameter from a list of rows.
    /// </summary>
    /// <remarks>
    /// Prefer using typed arrays (e.g., <c>int[]</c>, <c>string[]</c>, <c>DateTime?[]</c>) instead of <c>object[]</c>.
    /// </remarks>
    public static DbParameter ToArrayBindingParameter<TRow, TValue>(
        this IReadOnlyList<TRow> rows,
        string parameterName,
        DbDataType dataType,
        Func<TRow, TValue> selector,
        int? size = null)
    {
        global::AdoAsync.Validate.Required(rows, nameof(rows));
        global::AdoAsync.Validate.Required(parameterName, nameof(parameterName));
        global::AdoAsync.Validate.Required(selector, nameof(selector));
        if (rows.Count == 0)
        {
            throw new ArgumentException("Rows must not be empty for array binding.", nameof(rows));
        }

        if (RequiresSize(dataType) && !size.HasValue)
        {
            throw new ArgumentException(
                $"Array binding string parameters must specify Size. ParameterName='{parameterName}'.",
                nameof(size));
        }

        var values = new TValue[rows.Count];
        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            values[rowIndex] = selector(rows[rowIndex]);
        }

        return new DbParameter
        {
            Name = parameterName,
            DataType = dataType,
            Direction = ParameterDirection.Input,
            Value = values,
            Size = size,
            IsArrayBinding = true
        };
    }

    /// <summary>
    /// Creates a single Oracle array-binding parameter from a <see cref="DataTable"/> using a typed selector.
    /// </summary>
    /// <remarks>
    /// Use this instead of auto-converting by column name to avoid accidental "0 instead of null" behavior for value types.
    /// </remarks>
    public static DbParameter ToArrayBindingParameter<TValue>(
        this DataTable table,
        string parameterName,
        DbDataType dataType,
        Func<DataRow, TValue> selector,
        int? size = null)
    {
        global::AdoAsync.Validate.Required(table, nameof(table));
        global::AdoAsync.Validate.Required(parameterName, nameof(parameterName));
        global::AdoAsync.Validate.Required(selector, nameof(selector));
        if (table.Rows.Count == 0)
        {
            throw new ArgumentException("DataTable must have at least one row for array binding.", nameof(table));
        }

        if (RequiresSize(dataType) && !size.HasValue)
        {
            throw new ArgumentException(
                $"Array binding string parameters must specify Size. ParameterName='{parameterName}'.",
                nameof(size));
        }

        var values = new TValue[table.Rows.Count];
        for (var rowIndex = 0; rowIndex < table.Rows.Count; rowIndex++)
        {
            values[rowIndex] = selector(table.Rows[rowIndex]!);
        }

        return new DbParameter
        {
            Name = parameterName,
            DataType = dataType,
            Direction = ParameterDirection.Input,
            Value = values,
            Size = size,
            IsArrayBinding = true
        };
    }

    private static bool RequiresSize(DbDataType dataType) =>
        dataType is DbDataType.String
            or DbDataType.AnsiString
            or DbDataType.StringFixed
            or DbDataType.AnsiStringFixed;
}

