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
            return DbErrorMapper.Map(exception);
        }

        return ErrorRuleMatcher.Map(pgEx, Rules, ex => DbErrorMapper.Map(ex));
    }
    #endregion

    #region Helpers
    private static DbError Build(PostgresException exception, DbErrorType type, DbErrorCode code, bool isTransient, string messageKey)
    {
        return new DbError
        {
            Type = type,
            Code = code,
            MessageKey = messageKey,
            MessageParameters = new[] { exception.SqlState, exception.MessageText ?? exception.Message },
            IsTransient = isTransient,
            ProviderDetails = $"PostgresException#{exception.SqlState}"
        };
    }

    private static readonly ErrorRule<PostgresException>[] Rules =
    {
        // SQLSTATE values are stable across PostgreSQL versions.
        new(pg => pg.SqlState == PostgresErrorCodes.DeadlockDetected, pg => Build(pg, DbErrorType.Deadlock, DbErrorCode.GenericDeadlock, true, "errors.deadlock")),
        new(pg => pg.SqlState == PostgresErrorCodes.LockNotAvailable, pg => Build(pg, DbErrorType.ResourceLimit, DbErrorCode.ResourceLimitExceeded, true, "errors.resource_limit")),
        new(pg => pg.SqlState == PostgresErrorCodes.SerializationFailure, pg => Build(pg, DbErrorType.Deadlock, DbErrorCode.GenericDeadlock, true, "errors.deadlock")),
        new(pg => pg.SqlState == PostgresErrorCodes.QueryCanceled, pg => Build(pg, DbErrorType.Timeout, DbErrorCode.GenericTimeout, true, "errors.timeout")),
        new(pg => pg.SqlState == PostgresErrorCodes.ConnectionException, pg => Build(pg, DbErrorType.ConnectionFailure, DbErrorCode.ConnectionLost, true, "errors.connection_failure")),
        new(pg => pg.SqlState == PostgresErrorCodes.SyntaxError, pg => Build(pg, DbErrorType.SyntaxError, DbErrorCode.SyntaxError, false, "errors.syntax_error")),

        // Text-based fallback when SQLSTATE is missing/empty.
        new(pg => string.IsNullOrWhiteSpace(pg.SqlState) && (pg.MessageText ?? pg.Message).Contains("terminating connection", StringComparison.OrdinalIgnoreCase),
            pg => Build(pg, DbErrorType.ConnectionFailure, DbErrorCode.ConnectionLost, true, "errors.connection_failure"))
    };
    #endregion
}
