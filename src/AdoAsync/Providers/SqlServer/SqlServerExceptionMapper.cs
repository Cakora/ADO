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
            return DbErrorMapper.Map(exception);
        }

        return sqlEx.Number switch
        {
            // Map common SQL Server error numbers into stable categories.
            4060 or 18456 => DbErrorMapper.Unknown(sqlEx) with { Type = DbErrorType.ConnectionFailure, Code = DbErrorCode.ConnectionLost, IsTransient = true },
            1205 => DbErrorMapper.Unknown(sqlEx) with { Type = DbErrorType.Deadlock, Code = DbErrorCode.GenericDeadlock, IsTransient = true },
            10928 or 10929 => DbErrorMapper.Unknown(sqlEx) with { Type = DbErrorType.ResourceLimit, Code = DbErrorCode.ResourceLimitExceeded, IsTransient = true },
            -2 => DbErrorMapper.Unknown(sqlEx) with { Type = DbErrorType.Timeout, Code = DbErrorCode.GenericTimeout, IsTransient = true },
            _ => DbErrorMapper.Unknown(sqlEx)
        };
    }
    #endregion
}
