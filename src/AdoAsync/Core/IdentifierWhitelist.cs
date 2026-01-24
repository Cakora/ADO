using System;
using System.Collections.Generic;

namespace AdoAsync;

/// <summary>
/// Helper to enforce allow-list rules for identifiers.
/// Caller is responsible for supplying controlled allow lists when used.
/// </summary>
public static class IdentifierWhitelist
{
    #region Public API
    /// <summary>Throws if the identifier is not in the allow-list.</summary>
    public static void EnsureIdentifierAllowed(string identifier, IReadOnlySet<string> allowedIdentifiers)
    {
        Validate.Required(identifier, nameof(identifier));
        Validate.Required(allowedIdentifiers, nameof(allowedIdentifiers));

        if (!allowedIdentifiers.Contains(identifier))
        {
            throw new DbCallerException(DbErrorMapper.Validation($"Identifier '{identifier}' is not in the allowed list."));
        }
    }

    /// <summary>Throws if any identifier in the sequence is not in the allow-list.</summary>
    public static void EnsureIdentifiersAllowed(IEnumerable<string> identifiers, IReadOnlySet<string> allowedIdentifiers)
    {
        Validate.Required(identifiers, nameof(identifiers));
        Validate.Required(allowedIdentifiers, nameof(allowedIdentifiers));

        foreach (var identifier in identifiers)
        {
            // Fail fast on the first disallowed identifier for clearer errors.
            EnsureIdentifierAllowed(identifier, allowedIdentifiers);
        }
    }
    #endregion
}
