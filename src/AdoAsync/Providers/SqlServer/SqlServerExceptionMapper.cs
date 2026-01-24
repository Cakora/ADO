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

        return ErrorRuleMatcher.Map(sqlEx, Rules, ex => DbErrorMapper.Map(ex));
    }
    #endregion

    #region Helpers
    private static DbError Build(SqlException exception, DbErrorType type, DbErrorCode code, bool isTransient, string messageKey)
    {
        return new DbError
        {
            Type = type,
            Code = code,
            MessageKey = messageKey,
            MessageParameters = new[] { exception.Number.ToString(), exception.Message },
            IsTransient = isTransient,
            ProviderDetails = $"SqlException#{exception.Number}"
        };
    }

    private static readonly ErrorRule<SqlException>[] Rules =
    {
        // Numeric rules
        new(sql => sql.Number is 4060 or 18456, sql => Build(sql, DbErrorType.ConnectionFailure, DbErrorCode.ConnectionLost, true, "errors.authentication_failed")),
        new(sql => sql.Number == 1205, sql => Build(sql, DbErrorType.Deadlock, DbErrorCode.GenericDeadlock, true, "errors.deadlock")),
        new(sql => sql.Number is 10928 or 10929, sql => Build(sql, DbErrorType.ResourceLimit, DbErrorCode.ResourceLimitExceeded, true, "errors.resource_limit")),
        new(sql => sql.Number == -2, sql => Build(sql, DbErrorType.Timeout, DbErrorCode.GenericTimeout, true, "errors.timeout")),

        // Text-based rule for when no numeric code is set.
        new(sql => sql.Number == 0 && sql.Message.Contains("transport-level error", StringComparison.OrdinalIgnoreCase),
            sql => Build(sql, DbErrorType.ConnectionFailure, DbErrorCode.ConnectionLost, true, "errors.connection_failure"))
    };
    #endregion
}
