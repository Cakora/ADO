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
            // User-requested cancel or statement timeout.
            1013 => DbErrorMapper.Unknown(oraEx) with { Type = DbErrorType.Timeout, Code = DbErrorCode.GenericTimeout, IsTransient = true },
            // Network/response timeout.
            12170 => DbErrorMapper.Unknown(oraEx) with { Type = DbErrorType.Timeout, Code = DbErrorCode.GenericTimeout, IsTransient = true },
            // Listener/instance not available.
            12514 or 12541 => DbErrorMapper.Unknown(oraEx) with { Type = DbErrorType.ConnectionFailure, Code = DbErrorCode.ConnectionLost, IsTransient = true },
            // Resource limit exceeded (e.g., ORA-01000).
            1000 => DbErrorMapper.Unknown(oraEx) with { Type = DbErrorType.ResourceLimit, Code = DbErrorCode.ResourceLimitExceeded, IsTransient = false },
            // Default to unknown so callers can decide how to handle new codes.
            _ => DbErrorMapper.Unknown(oraEx)
        };
    }
    #endregion
}
