using System;
using System.Linq;

namespace AdoAsync;

/// <summary>
/// Minimal helper to evaluate ordered error rules without duplicating loops in provider mappers.
/// </summary>
internal static class ErrorRuleMatcher
{
    public static DbError Map<TException>(
        TException exception,
        ErrorRule<TException>[] rules,
        Func<TException, DbError> fallback)
    {
        var rule = rules.FirstOrDefault(r => r.Match(exception));
        if (rule is not null)
        {
            return rule.Map(exception);
        }

        return fallback(exception);
    }
}

/// <summary>Represents a single provider error rule.</summary>
internal sealed record ErrorRule<TException>(
    Func<TException, bool> Match,
    Func<TException, DbError> Map);
