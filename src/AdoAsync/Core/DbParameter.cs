using System.Data;

namespace AdoAsync;

/// <summary>
/// Database-agnostic parameter definition. Contains no execution logic.
/// </summary>
public sealed record DbParameter
{
    #region Members
    /// <summary>Parameter name (include prefix as required by provider).</summary>
    // Caller controls prefixes to keep provider rules explicit.
    public required string Name { get; init; }

    /// <summary>Cross-provider data type.</summary>
    public required DbDataType DataType { get; init; }

    /// <summary>Parameter direction.</summary>
    // Direction drives how values are read back after execution.
    public required ParameterDirection Direction { get; init; }

    /// <summary>Parameter value.</summary>
    public object? Value { get; init; }

    /// <summary>
    /// Size is mandatory for output string/binary parameters and encouraged for variable-length inputs when known.
    /// </summary>
    public int? Size { get; init; }

    /// <summary>Optional precision for decimals.</summary>
    public byte? Precision { get; init; }

    /// <summary>Optional scale for decimals.</summary>
    public byte? Scale { get; init; }

    /// <summary>
    /// Provider-specific structured type name (SQL Server TVP).
    /// </summary>
    /// <remarks>
    /// Required when <see cref="DataType"/> is <see cref="DbDataType.Structured"/>.
    /// Example: <c>dbo.MyRowType</c>.
    /// </remarks>
    public string? StructuredTypeName { get; init; }

    /// <summary>
    /// Indicates that this parameter uses provider array binding (Oracle PLSQL associative arrays).
    /// </summary>
    /// <remarks>
    /// When set, <see cref="Value"/> must be an array and all array-binding parameters in the same command must have the same length.
    /// </remarks>
    public bool IsArrayBinding { get; init; }
    #endregion
}
