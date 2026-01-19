using System;
using System.Data.Common;
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

    public DataConnection Create(DbConnection connection)
    {
        if (connection is null)
        {
            throw new ArgumentNullException(nameof(connection));
        }

        var provider = ResolveProvider();
        var options = new DataOptions().UseConnection(provider, connection, disposeConnection: false);
        return new DataConnection(options);
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
