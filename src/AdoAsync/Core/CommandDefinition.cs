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
    public CommandType CommandType { get; init; } = CommandType.Text;

    /// <summary>Parameters for the command.</summary>
    public IReadOnlyList<DbParameter>? Parameters { get; init; }

    /// <summary>Optional per-command timeout (seconds). If null, use default from options.</summary>
    public int? CommandTimeoutSeconds { get; init; }

    /// <summary>
    /// For readers: use default streaming-friendly behavior unless caller overrides.
    /// </summary>
    // Behavior is caller-controlled to keep streaming vs buffering explicit.
    public CommandBehavior Behavior { get; init; } = CommandBehavior.Default;

    /// <summary>
    /// Allow-list of stored procedures permitted for execution. Required when using stored procedures.
    /// </summary>
    // Keep allow-lists caller-owned so policies live outside the library.
    public IReadOnlySet<string>? AllowedStoredProcedures { get; init; }

    /// <summary>
    /// Allow-list of identifiers permitted for dynamic identifier usage (e.g., table names).
    /// </summary>
    // Identifiers are validated explicitly to avoid SQL injection via dynamic names.
    public IReadOnlySet<string>? AllowedIdentifiers { get; init; }

    /// <summary>
    /// Identifiers that will be used in the command and must be validated against <see cref="AllowedIdentifiers"/>.
    /// </summary>
    public IReadOnlyList<string>? IdentifiersToValidate { get; init; }
    #endregion
}
