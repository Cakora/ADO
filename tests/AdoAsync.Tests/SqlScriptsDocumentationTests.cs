using System;
using System.IO;
using FluentAssertions;
using Xunit;

namespace AdoAsync.Tests;

public class SqlScriptsDocumentationTests
{
    [Fact]
    public void Docs_IncludeBulkUpsertXmlScripts_ForSqlServerAndOracle()
    {
        var root = FindRepoRoot(AppContext.BaseDirectory);

        var sqlServerScript = Path.Combine(root, "docs", "sql-scripts", "bulk-upsert-by-name-sqlserver.sql");
        var oracleScript = Path.Combine(root, "docs", "sql-scripts", "bulk-upsert-by-name-oracle.sql");

        File.Exists(sqlServerScript).Should().BeTrue($"Expected SQL Server script at {sqlServerScript}");
        File.Exists(oracleScript).Should().BeTrue($"Expected Oracle script at {oracleScript}");
    }

    [Fact]
    public void Scripts_DoNotUseMerge_AndDocumentBatch500()
    {
        var root = FindRepoRoot(AppContext.BaseDirectory);

        var sqlServerScript = Path.Combine(root, "docs", "sql-scripts", "bulk-upsert-by-name-sqlserver.sql");
        var oracleScript = Path.Combine(root, "docs", "sql-scripts", "bulk-upsert-by-name-oracle.sql");

        var sqlText = File.ReadAllText(sqlServerScript);
        var oraText = File.ReadAllText(oracleScript);

        sqlText.Should().NotContain("MERGE INTO", "script should avoid MERGE statement");
        oraText.Should().NotContain("MERGE INTO", "script should avoid MERGE statement");

        sqlText.Should().Contain("500", "script should batch with size 500");
        oraText.Should().Contain("500", "script should batch with size 500");
    }

    private static string FindRepoRoot(string startDirectory)
    {
        var directory = new DirectoryInfo(startDirectory);
        while (directory is not null)
        {
            var slnPath = Path.Combine(directory.FullName, "AdoAsync.sln");
            if (File.Exists(slnPath))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repo root (AdoAsync.sln not found).");
    }
}
