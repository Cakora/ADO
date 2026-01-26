using System;
using System.Collections.Generic;
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

        var sqlState = pgEx.SqlState;
        if (!string.IsNullOrWhiteSpace(sqlState) && RulesBySqlState.TryGetValue(sqlState, out var rule))
        {
            return Build(pgEx, rule);
        }

        if (string.IsNullOrWhiteSpace(sqlState) && (pgEx.MessageText ?? pgEx.Message).Contains("terminating connection", StringComparison.OrdinalIgnoreCase))
        {
            return Build(pgEx, new Classification(DbErrorType.ConnectionFailure, DbErrorCodes.ConnectionLost, "errors.connection_failure"));
        }

        return DbErrorMapper.Map(pgEx);
    }
    #endregion

    #region Helpers
    private static DbError Build(PostgresException exception, Classification classification)
    {
        return DbErrorMapper.FromProvider(
            classification.Type,
            classification.Code,
            classification.MessageKey,
            new[] { exception.SqlState, exception.MessageText ?? exception.Message },
            classification.IsTransientOverride ?? DbErrorMapper.IsTransientByType(classification.Type),
            $"PostgresException#{exception.SqlState}");
    }

    private readonly record struct Classification(DbErrorType Type, string Code, string MessageKey, bool? IsTransientOverride = null);

    // Data-first list of retryable/typed PostgreSQL errors (SQLSTATE).
    private static readonly IReadOnlyDictionary<string, Classification> RulesBySqlState =
        new Dictionary<string, Classification>(StringComparer.Ordinal)
        {
            // Authentication failure (SQLSTATE).
            ["28P01"] = new(DbErrorType.ConnectionFailure, DbErrorCodes.AuthenticationFailed, "errors.authentication_failed", IsTransientOverride: false),

            [PostgresErrorCodes.DeadlockDetected] = new(DbErrorType.Deadlock, DbErrorCodes.GenericDeadlock, "errors.deadlock"),
            [PostgresErrorCodes.SerializationFailure] = new(DbErrorType.Deadlock, DbErrorCodes.GenericDeadlock, "errors.deadlock"),
            [PostgresErrorCodes.QueryCanceled] = new(DbErrorType.Timeout, DbErrorCodes.GenericTimeout, "errors.timeout"),
            [PostgresErrorCodes.ConnectionException] = new(DbErrorType.ConnectionFailure, DbErrorCodes.ConnectionLost, "errors.connection_failure"),

            // Not always “transient” in practice, but retry is typically safe for buffered reads/writes without a transaction.
            [PostgresErrorCodes.LockNotAvailable] = new(DbErrorType.ResourceLimit, DbErrorCodes.ResourceLimitExceeded, "errors.resource_limit"),

            [PostgresErrorCodes.SyntaxError] = new(DbErrorType.SyntaxError, DbErrorCodes.SyntaxError, "errors.syntax_error", IsTransientOverride: false)
        };
    #endregion
}
