using System;
using System.Data.Common;
using System.Globalization;
using LinqToDB;
using LinqToDB.Data;
using LinqToDB.DataProvider;
using LinqToDB.DataProvider.Oracle;
using LinqToDB.DataProvider.PostgreSQL;
using LinqToDB.DataProvider.SqlServer;

namespace AdoAsync.BulkCopy.LinqToDb.Common;

internal sealed class LinqToDbConnectionFactory
{
    private readonly DatabaseType _databaseType;

    public LinqToDbConnectionFactory(DatabaseType databaseType)
    {
        _databaseType = databaseType;
    }

    public DataConnection Create(DbConnection connection, DbTransaction? transaction)
    {
        if (connection is null)
        {
            throw new ArgumentNullException(nameof(connection));
        }

        var provider = ResolveProvider();
        var options = transaction is not null
            ? new DataOptions().UseTransaction(provider, transaction)
            : new DataOptions().UseConnection(provider, connection, disposeConnection: false);
        return new DataConnection(options);
    }

    public string? NormalizeTableName(string? tableName)
    {
        if (string.IsNullOrWhiteSpace(tableName))
        {
            return tableName;
        }

        if (_databaseType != DatabaseType.Oracle)
        {
            return tableName;
        }

        // Oracle folds unquoted identifiers to uppercase. Preserve quoted identifiers.
        if (tableName.Contains('"', StringComparison.Ordinal))
        {
            return tableName;
        }

        var parts = tableName.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
        {
            return tableName;
        }

        for (var i = 0; i < parts.Length; i++)
        {
            parts[i] = parts[i].ToUpper(CultureInfo.InvariantCulture);
        }

        return string.Join('.', parts);
    }

    private IDataProvider ResolveProvider()
    {
        return _databaseType switch
        {
            DatabaseType.SqlServer => SqlServerTools.GetDataProvider(),
            DatabaseType.PostgreSql => PostgreSQLTools.GetDataProvider(),
            DatabaseType.Oracle => OracleTools.GetDataProvider(),
            _ => throw new NotSupportedException($"Database type '{_databaseType}' is not supported by linq2db bulk copy.")
        };
    }
}
