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
    DbConnection CreateConnection(string connectionString);

    /// <summary>Creates a provider-specific command based on the definition.</summary>
    DbCommand CreateCommand(DbConnection connection, CommandDefinition definition);

    /// <summary>Applies parameters to a provider-specific command.</summary>
    // Separation keeps provider specifics out of the executor.
    void ApplyParameters(DbCommand command, IEnumerable<DbParameter> parameters);

    /// <summary>Performs a provider-specific bulk import and returns the inserted row count.</summary>
    ValueTask<int> BulkImportAsync(DbConnection connection, DbTransaction? transaction, BulkImportRequest request, CancellationToken cancellationToken = default);
    #endregion
}
