using System.Data;

namespace AdoAsync.Providers.SqlServer;

/// <summary>
/// SQL Server parameter helpers (TVP).
/// </summary>
public static class SqlServerParameterExtensions
{
    /// <summary>
    /// Creates a SQL Server table-valued parameter (TVP) from a <see cref="DataTable"/>.
    /// </summary>
    public static DbParameter ToTvp(this DataTable table, string parameterName, string structuredTypeName)
    {
        global::AdoAsync.Validate.Required(table, nameof(table));
        global::AdoAsync.Validate.Required(parameterName, nameof(parameterName));
        global::AdoAsync.Validate.Required(structuredTypeName, nameof(structuredTypeName));

        return new DbParameter
        {
            Name = parameterName,
            DataType = DbDataType.Structured,
            Direction = ParameterDirection.Input,
            StructuredTypeName = structuredTypeName,
            Value = table
        };
    }
}

