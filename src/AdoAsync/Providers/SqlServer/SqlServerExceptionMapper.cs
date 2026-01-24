using System;
using System.Collections.Generic;
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

        if (RulesByNumber.TryGetValue(sqlEx.Number, out var rule))
        {
            return Build(sqlEx, rule);
        }

        if (sqlEx.Number == 0 && sqlEx.Message.Contains("transport-level error", StringComparison.OrdinalIgnoreCase))
        {
            return Build(sqlEx, new Classification(DbErrorType.ConnectionFailure, DbErrorCode.ConnectionLost, "errors.connection_failure"));
        }

        return DbErrorMapper.Map(sqlEx);
    }
    #endregion

    #region Helpers
    private static DbError Build(SqlException exception, Classification classification)
    {
        return DbErrorMapper.FromProvider(
            classification.Type,
            classification.Code,
            classification.MessageKey,
            new[] { exception.Number.ToString(), exception.Message },
            classification.IsTransientOverride ?? DbErrorMapper.IsTransientByType(classification.Type),
            $"SqlException#{exception.Number}");
    }

    private readonly record struct Classification(DbErrorType Type, DbErrorCode Code, string MessageKey, bool? IsTransientOverride = null);

    // Data-first list of retryable/typed SQL Server errors.
    private static readonly IReadOnlyDictionary<int, Classification> RulesByNumber = new Dictionary<int, Classification>
    {
        // Login/connection issues
        [4060] = new(DbErrorType.ConnectionFailure, DbErrorCode.ConnectionLost, "errors.authentication_failed", IsTransientOverride: false),
        [18456] = new(DbErrorType.ConnectionFailure, DbErrorCode.ConnectionLost, "errors.authentication_failed", IsTransientOverride: false),

        // Deadlock
        [1205] = new(DbErrorType.Deadlock, DbErrorCode.GenericDeadlock, "errors.deadlock"),

        // Resource throttling
        [10928] = new(DbErrorType.ResourceLimit, DbErrorCode.ResourceLimitExceeded, "errors.resource_limit"),
        [10929] = new(DbErrorType.ResourceLimit, DbErrorCode.ResourceLimitExceeded, "errors.resource_limit"),

        // Timeout
        [-2] = new(DbErrorType.Timeout, DbErrorCode.GenericTimeout, "errors.timeout")
    };
    #endregion
}
