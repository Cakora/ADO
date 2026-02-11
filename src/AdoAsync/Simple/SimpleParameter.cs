using System.Data;

namespace AdoAsync.Simple;

/// <summary>Simple parameter definition for standalone helpers.</summary>
public sealed class SimpleParameter
{
    /// <summary>Create an input parameter (Direction defaults to Input).</summary>
    public SimpleParameter(string name, object? value)
    {
        Name = name;
        Value = value;
        Direction = ParameterDirection.Input;
    }

    /// <summary>Create a parameter with explicit type/direction (used for Output/RefCursor).</summary>
    public SimpleParameter(string name, DbDataType dataType, ParameterDirection direction, int? size = null)
    {
        Name = name;
        DataType = dataType;
        Direction = direction;
        Size = size;
    }

    /// <summary>Parameter name.</summary>
    public string Name { get; }
    /// <summary>Input value (if any).</summary>
    public object? Value { get; }
    /// <summary>Optional data type (required for output/refcursor).</summary>
    public DbDataType? DataType { get; }
    /// <summary>Parameter direction.</summary>
    public ParameterDirection Direction { get; }
    /// <summary>Optional size for string/binary outputs.</summary>
    public int? Size { get; }
}
