using System;
using System.Collections.Generic;

namespace AdoAsync;

/// <summary>
/// Helper to enforce allow-list rules for stored procedure names and identifiers.
/// Caller is responsible for supplying controlled allow lists.
/// </summary>
public static class IdentifierWhitelist
{
    #region Public API
    /// <summary>Throws if the stored procedure is not in the allow-list.</summary>
    public static void EnsureStoredProcedureAllowed(string procedureName, IReadOnlySet<string> allowedProcedures)
    {
        Validate.Required(procedureName, nameof(procedureName));
        Validate.Required(allowedProcedures, nameof(allowedProcedures));

        if (!allowedProcedures.Contains(procedureName))
        {
            throw new DatabaseException(ErrorCategory.Validation, $"Stored procedure '{procedureName}' is not in the allowed list.");
        }
    }

    /// <summary>Throws if the identifier is not in the allow-list.</summary>
    public static void EnsureIdentifierAllowed(string identifier, IReadOnlySet<string> allowedIdentifiers)
    {
        Validate.Required(identifier, nameof(identifier));
        Validate.Required(allowedIdentifiers, nameof(allowedIdentifiers));

        if (!allowedIdentifiers.Contains(identifier))
        {
            throw new DatabaseException(ErrorCategory.Validation, $"Identifier '{identifier}' is not in the allowed list.");
        }
    }

    /// <summary>Throws if any identifier in the sequence is not in the allow-list.</summary>
    public static void EnsureIdentifiersAllowed(IEnumerable<string> identifiers, IReadOnlySet<string> allowedIdentifiers)
    {
        Validate.Required(identifiers, nameof(identifiers));
        Validate.Required(allowedIdentifiers, nameof(allowedIdentifiers));

        foreach (var identifier in identifiers)
        {
            EnsureIdentifierAllowed(identifier, allowedIdentifiers);
        }
    }
    #endregion
}
