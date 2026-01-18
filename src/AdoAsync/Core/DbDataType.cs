namespace AdoAsync;

/// <summary>
/// Cross-provider data types for parameters.
/// </summary>
public enum DbDataType
{
    #region Values
    /// <summary>Unicode variable-length string.</summary>
    String,
    /// <summary>ANSI variable-length string.</summary>
    AnsiString,
    /// <summary>Unicode fixed-length string.</summary>
    StringFixed,
    /// <summary>ANSI fixed-length string.</summary>
    AnsiStringFixed,
    /// <summary>Large text (CLOB).</summary>
    Clob,
    /// <summary>Large Unicode text (NCLOB).</summary>
    NClob,

    /// <summary>16-bit integer.</summary>
    Int16,
    /// <summary>32-bit integer.</summary>
    Int32,
    /// <summary>64-bit integer.</summary>
    Int64,
    /// <summary>Unsigned byte.</summary>
    Byte,
    /// <summary>Signed byte.</summary>
    SByte,
    /// <summary>Unsigned 16-bit integer.</summary>
    UInt16,
    /// <summary>Unsigned 32-bit integer.</summary>
    UInt32,
    /// <summary>Unsigned 64-bit integer.</summary>
    UInt64,
    /// <summary>Decimal number with precision/scale.</summary>
    Decimal,
    /// <summary>Double precision floating point.</summary>
    Double,
    /// <summary>Single precision floating point.</summary>
    Single,
    /// <summary>Currency/money type.</summary>
    Currency,

    /// <summary>Boolean value.</summary>
    Boolean,

    /// <summary>GUID/UUID.</summary>
    Guid,

    /// <summary>Binary data.</summary>
    Binary,
    /// <summary>Large binary object.</summary>
    Blob,

    /// <summary>Date only.</summary>
    Date,
    /// <summary>Time only.</summary>
    Time,
    /// <summary>Legacy date+time.</summary>
    DateTime,
    /// <summary>Preferred precise date+time.</summary>
    DateTime2,
    /// <summary>Date+time with offset.</summary>
    DateTimeOffset,
    /// <summary>Rowversion/timestamp.</summary>
    Timestamp,
    /// <summary>Interval/duration.</summary>
    Interval,

    /// <summary>JSON payload.</summary>
    Json,
    /// <summary>XML payload.</summary>
    Xml
    #endregion
}
