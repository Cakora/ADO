using System;
using System.Collections.Generic;
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
            return DbErrorMapper.Map(exception);
        }

        if (RulesByNumber.TryGetValue(oraEx.Number, out var rule))
        {
            return Build(oraEx, rule);
        }

        if (oraEx.Number == 0 && oraEx.Message.Contains("broken pipe", StringComparison.OrdinalIgnoreCase))
        {
            return Build(oraEx, new Classification(DbErrorType.ConnectionFailure, DbErrorCodes.ConnectionLost, "errors.connection_failure"));
        }

        return DbErrorMapper.Map(oraEx);
    }
    #endregion

    #region Helpers
    private static DbError Build(OracleException exception, Classification classification)
    {
        return DbErrorMapper.FromProvider(
            classification.Type,
            classification.Code,
            classification.MessageKey,
            new[] { exception.Number.ToString(), exception.Message },
            classification.IsTransientOverride ?? DbErrorMapper.IsTransientByType(classification.Type),
            $"OracleException#{exception.Number}");
    }

    private readonly record struct Classification(DbErrorType Type, string Code, string MessageKey, bool? IsTransientOverride = null);

    // Data-first list of retryable/typed Oracle errors (ORA-xxxxx).
    private static readonly IReadOnlyDictionary<int, Classification> RulesByNumber = new Dictionary<int, Classification>
    {
        // ORA-01017: invalid username/password.
        [1017] = new(DbErrorType.ConnectionFailure, DbErrorCodes.AuthenticationFailed, "errors.authentication_failed", IsTransientOverride: false),
        [1013] = new(DbErrorType.Timeout, DbErrorCodes.GenericTimeout, "errors.timeout"),
        [12170] = new(DbErrorType.Timeout, DbErrorCodes.GenericTimeout, "errors.timeout"),
        [12514] = new(DbErrorType.ConnectionFailure, DbErrorCodes.ConnectionLost, "errors.connection_failure"),
        [12541] = new(DbErrorType.ConnectionFailure, DbErrorCodes.ConnectionLost, "errors.connection_failure"),

        // ORA-01000: maximum open cursors exceeded (not typically resolved by immediate retry).
        [1000] = new(DbErrorType.ResourceLimit, DbErrorCodes.ResourceLimitExceeded, "errors.resource_limit", IsTransientOverride: false)
    };
    #endregion
}
