using System.Collections.Generic;
using System.Data;

namespace AdoAsync;

/// <summary>
/// Command definition for text and stored procedures. No execution logic lives here.
/// </summary>
public sealed record CommandDefinition
{
    #region Members
    /// <summary>SQL text or stored procedure name.</summary>
    public required string CommandText { get; init; }

    /// <summary>Command type (text or stored procedure).</summary>
    // Avoid implicit detection so call sites are explicit.
    public CommandType CommandType { get; init; } = CommandType.Text;

    /// <summary>Parameters for the command.</summary>
    public IReadOnlyList<DbParameter>? Parameters { get; init; }

    /// <summary>Optional per-command timeout (seconds). If null, use default from options.</summary>
    public int? CommandTimeoutSeconds { get; init; }

    /// <summary>
    /// For readers: use default streaming-friendly behavior unless caller overrides.
    /// </summary>
    // Keeps streaming the default unless a caller opts into buffering behaviors.
    public CommandBehavior Behavior { get; init; } = CommandBehavior.Default;

    /// <summary>
    /// Allow-list of identifiers permitted for dynamic identifier usage (e.g., table names).
    /// </summary>
    public IReadOnlySet<string>? AllowedIdentifiers { get; init; }

    /// <summary>
    /// Identifiers that will be used in the command and must be validated against <see cref="AllowedIdentifiers"/>.
    /// </summary>
    public IReadOnlyList<string>? IdentifiersToValidate { get; init; }

    #endregion
}
