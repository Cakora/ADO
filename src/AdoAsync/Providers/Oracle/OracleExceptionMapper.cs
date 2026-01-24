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
            return DbErrorMapper.Map(exception);
        }

        return ErrorRuleMatcher.Map(oraEx, Rules, ex => DbErrorMapper.Map(ex));
    }
    #endregion

    #region Helpers
    private static DbError Build(OracleException exception, DbErrorType type, DbErrorCode code, bool isTransient, string messageKey)
    {
        return new DbError
        {
            Type = type,
            Code = code,
            MessageKey = messageKey,
            MessageParameters = new[] { exception.Number.ToString(), exception.Message },
            IsTransient = isTransient,
            ProviderDetails = $"OracleException#{exception.Number}"
        };
    }

    private static readonly ErrorRule<OracleException>[] Rules =
    {
        // Oracle error numbers are the most reliable cross-version signal.
        new(ora => ora.Number == 1013, ora => Build(ora, DbErrorType.Timeout, DbErrorCode.GenericTimeout, true, "errors.timeout")),
        new(ora => ora.Number == 12170, ora => Build(ora, DbErrorType.Timeout, DbErrorCode.GenericTimeout, true, "errors.timeout")),
        new(ora => ora.Number is 12514 or 12541, ora => Build(ora, DbErrorType.ConnectionFailure, DbErrorCode.ConnectionLost, true, "errors.connection_failure")),
        new(ora => ora.Number == 1000, ora => Build(ora, DbErrorType.ResourceLimit, DbErrorCode.ResourceLimitExceeded, false, "errors.resource_limit")),

        // Text-based fallback when no reliable number is present.
        new(ora => ora.Number == 0 && ora.Message.Contains("broken pipe", StringComparison.OrdinalIgnoreCase),
            ora => Build(ora, DbErrorType.ConnectionFailure, DbErrorCode.ConnectionLost, true, "errors.connection_failure"))
    };
    #endregion
}
