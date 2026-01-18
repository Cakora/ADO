using System;
using NpgsqlTypes;

namespace AdoAsync.Providers.PostgreSql;

/// <summary>
/// Maps DbDataType to NpgsqlDbType.
/// </summary>
public static class PostgreSqlTypeMapper
{
    #region Public API
    /// <summary>Returns the NpgsqlDbType for the provided DbDataType.</summary>
    public static NpgsqlDbType Map(DbDataType type) =>
        type switch
        {
            DbDataType.String => NpgsqlDbType.Text,
            DbDataType.AnsiString => NpgsqlDbType.Text,
            DbDataType.StringFixed => NpgsqlDbType.Char,
            DbDataType.AnsiStringFixed => NpgsqlDbType.Char,
            DbDataType.Clob => NpgsqlDbType.Text,
            DbDataType.NClob => NpgsqlDbType.Text,

            DbDataType.Int16 => NpgsqlDbType.Smallint,
            DbDataType.Int32 => NpgsqlDbType.Integer,
            DbDataType.Int64 => NpgsqlDbType.Bigint,
            DbDataType.Byte => NpgsqlDbType.Smallint,
            DbDataType.SByte => NpgsqlDbType.Smallint,
            DbDataType.UInt16 => NpgsqlDbType.Integer,
            DbDataType.UInt32 => NpgsqlDbType.Bigint,
            DbDataType.UInt64 => NpgsqlDbType.Numeric,
            DbDataType.Decimal => NpgsqlDbType.Numeric,
            DbDataType.Double => NpgsqlDbType.Double,
            DbDataType.Single => NpgsqlDbType.Real,
            DbDataType.Currency => NpgsqlDbType.Money,

            DbDataType.Boolean => NpgsqlDbType.Boolean,
            DbDataType.Guid => NpgsqlDbType.Uuid,

            DbDataType.Binary => NpgsqlDbType.Bytea,
            DbDataType.Blob => NpgsqlDbType.Bytea,

            DbDataType.Date => NpgsqlDbType.Date,
            DbDataType.Time => NpgsqlDbType.Time,
            DbDataType.DateTime => NpgsqlDbType.Timestamp,
            DbDataType.DateTime2 => NpgsqlDbType.TimestampTz,
            DbDataType.DateTimeOffset => NpgsqlDbType.TimestampTz,
            DbDataType.Timestamp => NpgsqlDbType.Timestamp,
            DbDataType.Interval => NpgsqlDbType.Interval,

            DbDataType.Json => NpgsqlDbType.Jsonb,
            DbDataType.Xml => NpgsqlDbType.Xml,

            _ => throw new NotSupportedException($"DbDataType '{type}' is not supported.")
        };
    #endregion
}
