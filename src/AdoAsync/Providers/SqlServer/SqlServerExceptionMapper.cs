using System;
using Microsoft.Data.SqlClient;

namespace AdoAsync.Providers.SqlServer;

/// <summary>
/// Translates SQL Server exceptions to DbError categories/codes.
/// </summary>
public static class SqlServerExceptionMapper
{
    #region Public API
    /// <summary>Maps SQL exceptions to provider-agnostic errors.</summary>
    public static DbError Map(Exception exception)
    {
        if (exception is not SqlException sqlEx)
        {
            return DbErrorMapper.Unknown(exception);
        }

        return sqlEx.Number switch
        {
            // Auth/login or database not found.
            4060 or 18456 => DbErrorMapper.Unknown(sqlEx) with { Type = DbErrorType.ConnectionFailure, Code = DbErrorCode.ConnectionLost, IsTransient = true },
            // Deadlock victim.
            1205 => DbErrorMapper.Unknown(sqlEx) with { Type = DbErrorType.Deadlock, Code = DbErrorCode.GenericDeadlock, IsTransient = true },
            // Azure SQL throttling/resource limits.
            10928 or 10929 => DbErrorMapper.Unknown(sqlEx) with { Type = DbErrorType.ResourceLimit, Code = DbErrorCode.ResourceLimitExceeded, IsTransient = true },
            // ADO.NET timeout.
            -2 => DbErrorMapper.Unknown(sqlEx) with { Type = DbErrorType.Timeout, Code = DbErrorCode.GenericTimeout, IsTransient = true },
            // Leave unknown SQL errors untouched so callers can inspect ProviderDetails.
            _ => DbErrorMapper.Unknown(sqlEx)
        };
    }
    #endregion
}
