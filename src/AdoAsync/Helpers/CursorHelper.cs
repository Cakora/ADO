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
}
