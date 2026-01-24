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
            return Build(oraEx, new Classification(DbErrorType.ConnectionFailure, DbErrorCode.ConnectionLost, true, "errors.connection_failure"));
        }

        return DbErrorMapper.Map(oraEx);
    }
    #endregion

    #region Helpers
    private static DbError Build(OracleException exception, Classification classification)
    {
        return new DbError
        {
            Type = classification.Type,
            Code = classification.Code,
            MessageKey = classification.MessageKey,
            MessageParameters = new[] { exception.Number.ToString(), exception.Message },
            IsTransient = classification.IsTransient,
            ProviderDetails = $"OracleException#{exception.Number}"
        };
    }

    private readonly record struct Classification(DbErrorType Type, DbErrorCode Code, bool IsTransient, string MessageKey);

    // Data-first list of retryable/typed Oracle errors (ORA-xxxxx).
    private static readonly IReadOnlyDictionary<int, Classification> RulesByNumber = new Dictionary<int, Classification>
    {
        [1013] = new(DbErrorType.Timeout, DbErrorCode.GenericTimeout, true, "errors.timeout"),
        [12170] = new(DbErrorType.Timeout, DbErrorCode.GenericTimeout, true, "errors.timeout"),
        [12514] = new(DbErrorType.ConnectionFailure, DbErrorCode.ConnectionLost, true, "errors.connection_failure"),
        [12541] = new(DbErrorType.ConnectionFailure, DbErrorCode.ConnectionLost, true, "errors.connection_failure"),

        // ORA-01000: maximum open cursors exceeded (not typically resolved by immediate retry).
        [1000] = new(DbErrorType.ResourceLimit, DbErrorCode.ResourceLimitExceeded, false, "errors.resource_limit")
    };
    #endregion
}
