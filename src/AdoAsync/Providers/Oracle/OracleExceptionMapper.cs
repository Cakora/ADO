using System;
using Oracle.ManagedDataAccess.Client;

namespace AdoAsync.Providers.Oracle;

/// <summary>
/// Translates Oracle exceptions to DbError categories/codes.
/// </summary>
public static class OracleExceptionMapper
{
    #region Public API
    /// <summary>Maps Oracle exceptions to provider-agnostic errors.</summary>
    public static DbError Map(Exception exception)
    {
        if (exception is not OracleException oraEx)
        {
            return DbErrorMapper.Unknown(exception);
        }

        return oraEx.Number switch
        {
            1013 => DbErrorMapper.Unknown(oraEx) with { Type = DbErrorType.Timeout, Code = DbErrorCode.GenericTimeout, IsTransient = true },
            12170 => DbErrorMapper.Unknown(oraEx) with { Type = DbErrorType.Timeout, Code = DbErrorCode.GenericTimeout, IsTransient = true },
            12514 or 12541 => DbErrorMapper.Unknown(oraEx) with { Type = DbErrorType.ConnectionFailure, Code = DbErrorCode.ConnectionLost, IsTransient = true },
            1000 => DbErrorMapper.Unknown(oraEx) with { Type = DbErrorType.ResourceLimit, Code = DbErrorCode.ResourceLimitExceeded, IsTransient = false },
            _ => DbErrorMapper.Unknown(oraEx)
        };
    }
    #endregion
}
