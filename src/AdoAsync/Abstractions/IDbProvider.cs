using System.Collections.Generic;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;

namespace AdoAsync.Abstractions;

/// <summary>
/// Provider-specific factory and bindings. Contains no implementation logic here.
/// </summary>
public interface IDbProvider
{
    #region Members
    /// <summary>Creates a provider-specific connection.</summary>
    // Keeps provider specifics behind a thin interface for a stable facade.
    DbConnection CreateConnection(string connectionString);

    /// <summary>Creates a provider-specific command based on the definition.</summary>
    DbCommand CreateCommand(DbConnection connection, CommandDefinition definition);

    /// <summary>Applies parameters to a provider-specific command.</summary>
    // Parameter mapping stays provider-specific to avoid leaking provider types into the facade.
    void ApplyParameters(DbCommand command, IEnumerable<DbParameter> parameters);

    /// <summary>Performs a provider-specific bulk import and returns the inserted row count.</summary>
    // Keep bulk import here so provider-specific APIs stay isolated from the facade.
    ValueTask<int> BulkImportAsync(DbConnection connection, BulkImportRequest request, CancellationToken cancellationToken = default);
    #endregion
}
