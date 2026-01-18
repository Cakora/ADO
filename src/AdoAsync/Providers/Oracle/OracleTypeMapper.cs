using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using Oracle.ManagedDataAccess.Client;

namespace AdoAsync.Providers.Oracle;

/// <summary>
/// Maps DbDataType to OracleDbType.
/// </summary>
public static class OracleTypeMapper
{
    // FrozenDictionary avoids repeated allocations on hot mapping paths.
    private static readonly FrozenDictionary<DbDataType, OracleDbType> MapTable =
        new Dictionary<DbDataType, OracleDbType>
        {
            [DbDataType.String] = OracleDbType.NVarchar2,
            [DbDataType.AnsiString] = OracleDbType.Varchar2,
            [DbDataType.StringFixed] = OracleDbType.NChar,
            [DbDataType.AnsiStringFixed] = OracleDbType.Char,
            [DbDataType.Clob] = OracleDbType.Clob,
            [DbDataType.NClob] = OracleDbType.NClob,

            [DbDataType.Int16] = OracleDbType.Int16,
            [DbDataType.Int32] = OracleDbType.Int32,
            [DbDataType.Int64] = OracleDbType.Int64,
            [DbDataType.Byte] = OracleDbType.Byte,
            [DbDataType.SByte] = OracleDbType.Byte,
            [DbDataType.UInt16] = OracleDbType.Int32,
            [DbDataType.UInt32] = OracleDbType.Int64,
            // Unsigned 64-bit maps to decimal to preserve range.
            [DbDataType.UInt64] = OracleDbType.Decimal,
            [DbDataType.Decimal] = OracleDbType.Decimal,
            [DbDataType.Double] = OracleDbType.Double,
            [DbDataType.Single] = OracleDbType.Single,
            [DbDataType.Currency] = OracleDbType.Decimal,

            [DbDataType.Boolean] = OracleDbType.Byte,
            [DbDataType.Guid] = OracleDbType.Raw,

            [DbDataType.Binary] = OracleDbType.Raw,
            [DbDataType.Blob] = OracleDbType.Blob,

            [DbDataType.Date] = OracleDbType.Date,
            [DbDataType.Time] = OracleDbType.TimeStamp,
            [DbDataType.DateTime] = OracleDbType.TimeStamp,
            [DbDataType.DateTime2] = OracleDbType.TimeStamp,
            [DbDataType.DateTimeOffset] = OracleDbType.TimeStampTZ,
            [DbDataType.Timestamp] = OracleDbType.TimeStamp,
            [DbDataType.Interval] = OracleDbType.IntervalDS,

            [DbDataType.Json] = OracleDbType.NVarchar2,
            [DbDataType.Xml] = OracleDbType.XmlType,
            // RefCursor is required for Oracle multi-result procedures.
            [DbDataType.RefCursor] = OracleDbType.RefCursor
        }.ToFrozenDictionary();

    #region Public API
    /// <summary>Returns the OracleDbType for the provided DbDataType.</summary>
    public static OracleDbType Map(DbDataType type) =>
        MapTable.TryGetValue(type, out var mapped)
            ? mapped
            : throw new DatabaseException(ErrorCategory.Unsupported, $"DbDataType '{type}' is not supported.");
    #endregion
}
