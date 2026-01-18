using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Data;
using Microsoft.Data.SqlClient;

namespace AdoAsync.Providers.SqlServer;

/// <summary>
/// Maps DbDataType to SqlDbType.
/// </summary>
public static class SqlServerTypeMapper
{
    private static readonly FrozenDictionary<DbDataType, SqlDbType> MapTable =
        new Dictionary<DbDataType, SqlDbType>
        {
            [DbDataType.String] = SqlDbType.NVarChar,
            [DbDataType.AnsiString] = SqlDbType.VarChar,
            [DbDataType.StringFixed] = SqlDbType.NChar,
            [DbDataType.AnsiStringFixed] = SqlDbType.Char,
            [DbDataType.Clob] = SqlDbType.NVarChar,
            [DbDataType.NClob] = SqlDbType.NVarChar,

            [DbDataType.Int16] = SqlDbType.SmallInt,
            [DbDataType.Int32] = SqlDbType.Int,
            [DbDataType.Int64] = SqlDbType.BigInt,
            [DbDataType.Byte] = SqlDbType.TinyInt,
            [DbDataType.SByte] = SqlDbType.TinyInt, // store as tinyint with validation upstream
            [DbDataType.UInt16] = SqlDbType.Int, // unsupported unsigned; map to larger signed with validation upstream
            [DbDataType.UInt32] = SqlDbType.BigInt,
            [DbDataType.UInt64] = SqlDbType.Decimal,
            [DbDataType.Decimal] = SqlDbType.Decimal,
            [DbDataType.Double] = SqlDbType.Float,
            [DbDataType.Single] = SqlDbType.Real,
            [DbDataType.Currency] = SqlDbType.Money,

            [DbDataType.Boolean] = SqlDbType.Bit,
            [DbDataType.Guid] = SqlDbType.UniqueIdentifier,

            [DbDataType.Binary] = SqlDbType.VarBinary,
            [DbDataType.Blob] = SqlDbType.VarBinary,

            [DbDataType.Date] = SqlDbType.Date,
            [DbDataType.Time] = SqlDbType.Time,
            [DbDataType.DateTime] = SqlDbType.DateTime,
            [DbDataType.DateTime2] = SqlDbType.DateTime2,
            [DbDataType.DateTimeOffset] = SqlDbType.DateTimeOffset,
            [DbDataType.Timestamp] = SqlDbType.Timestamp,
            [DbDataType.Interval] = SqlDbType.Time,

            [DbDataType.Json] = SqlDbType.NVarChar,
            [DbDataType.Xml] = SqlDbType.Xml
        }.ToFrozenDictionary();

    #region Public API
    /// <summary>Returns the SqlDbType for the provided DbDataType.</summary>
    public static SqlDbType Map(DbDataType type) =>
        MapTable.TryGetValue(type, out var mapped)
            ? mapped
            : throw new DatabaseException(ErrorCategory.Unsupported, $"DbDataType '{type}' is not supported.");
    #endregion
}
