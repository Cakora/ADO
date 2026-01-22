using System.Collections.Generic;
using System.Data.Common;
using System.Data;
using System.Linq;

namespace AdoAsync.Helpers;

internal static class CursorHelper
{
    /// <summary>Check if the command targets an Oracle refcursor (requires buffered handling).</summary>
    /// <param name="databaseType">Target database provider.</param>
    /// <param name="command">Command definition with parameters.</param>
    /// <returns>True when the command includes an Oracle refcursor output.</returns>
    public static bool IsOracleRefCursor(DatabaseType databaseType, CommandDefinition command) =>
        databaseType == DatabaseType.Oracle
        && command.CommandType == CommandType.StoredProcedure
        && command.Parameters is { Count: > 0 }
        && command.Parameters.Any(p => p.DataType == DbDataType.RefCursor);

    /// <summary>Check if the command targets a PostgreSQL refcursor (requires buffered handling).</summary>
    /// <param name="databaseType">Target database provider.</param>
    /// <param name="command">Command definition with parameters.</param>
    /// <returns>True when the command includes a PostgreSQL refcursor output.</returns>
    public static bool IsPostgresRefCursor(DatabaseType databaseType, CommandDefinition command) =>
        databaseType == DatabaseType.PostgreSql
        && command.CommandType == CommandType.StoredProcedure
        && command.Parameters is { Count: > 0 }
        && command.Parameters.Any(p => p.DataType == DbDataType.RefCursor);

    /// <summary>Collect output refcursor names from a PostgreSQL command after execution.</summary>
    /// <param name="dbCommand">Executed command containing provider parameters.</param>
    /// <returns>List of cursor names returned by output parameters.</returns>
    public static List<string> CollectPostgresCursorNames(DbCommand dbCommand)
    {
        var names = new List<string>();
        foreach (System.Data.Common.DbParameter parameter in dbCommand.Parameters)
        {
            if (parameter.Direction == ParameterDirection.Output
                && parameter.Value is string cursorName
                && !string.IsNullOrWhiteSpace(cursorName))
            {
                names.Add(cursorName);
            }
        }

        return names;
    }
}
