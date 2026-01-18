using System;
using Npgsql;

namespace AdoAsync.Providers.PostgreSql;

/// <summary>
/// Translates PostgreSQL exceptions to DbError categories/codes.
/// </summary>
public static class PostgreSqlExceptionMapper
{
    #region Public API
    /// <summary>Maps Npgsql exceptions to provider-agnostic errors.</summary>
    public static DbError Map(Exception exception)
    {
        if (exception is not PostgresException pgEx)
        {
            return DbErrorMapper.Unknown(exception);
        }

        return pgEx.SqlState switch
        {
            PostgresErrorCodes.DeadlockDetected => DbErrorMapper.Unknown(pgEx) with { Type = DbErrorType.Deadlock, Code = DbErrorCode.GenericDeadlock, IsTransient = true },
            PostgresErrorCodes.LockNotAvailable => DbErrorMapper.Unknown(pgEx) with { Type = DbErrorType.ResourceLimit, Code = DbErrorCode.ResourceLimitExceeded, IsTransient = true },
            PostgresErrorCodes.SerializationFailure => DbErrorMapper.Unknown(pgEx) with { Type = DbErrorType.Deadlock, Code = DbErrorCode.GenericDeadlock, IsTransient = true },
            PostgresErrorCodes.QueryCanceled => DbErrorMapper.Unknown(pgEx) with { Type = DbErrorType.Timeout, Code = DbErrorCode.GenericTimeout, IsTransient = true },
            PostgresErrorCodes.ConnectionException => DbErrorMapper.Unknown(pgEx) with { Type = DbErrorType.ConnectionFailure, Code = DbErrorCode.ConnectionLost, IsTransient = true },
            PostgresErrorCodes.SyntaxError => DbErrorMapper.Unknown(pgEx) with { Type = DbErrorType.SyntaxError, Code = DbErrorCode.SyntaxError, IsTransient = false },
            _ => DbErrorMapper.Unknown(pgEx)
        };
    }
    #endregion
}
