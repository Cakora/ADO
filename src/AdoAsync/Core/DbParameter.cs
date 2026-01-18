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
    #endregion
}
