using System;
using System.Globalization;

namespace AdoAsync;

internal static class IdentifierNormalization
{
    internal static string NormalizeTableName(DatabaseType databaseType, string tableName)
    {
        if (string.IsNullOrWhiteSpace(tableName))
        {
            return tableName;
        }

        if (databaseType != DatabaseType.Oracle)
        {
            return tableName;
        }

        // Oracle folds unquoted identifiers to uppercase. Preserve quoted identifiers.
        if (tableName.Contains('"', StringComparison.Ordinal))
        {
            return tableName;
        }

        var parts = tableName.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
        {
            return tableName;
        }

        for (var i = 0; i < parts.Length; i++)
        {
            parts[i] = parts[i].ToUpper(CultureInfo.InvariantCulture);
        }

        return string.Join('.', parts);
    }
}

