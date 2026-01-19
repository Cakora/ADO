using System;
using System.IO;
using FluentAssertions;
using Xunit;

namespace AdoAsync.Tests;

public class MultiResultDocumentationTests
{
    [Fact]
    public void Readme_IncludesMultiResultSection_ForAllProviders()
    {
        var root = FindRepoRoot(AppContext.BaseDirectory);
        var readmePath = Path.Combine(root, "src", "AdoAsync", "README.md");

        File.Exists(readmePath).Should().BeTrue($"README should exist at {readmePath}");
        var text = File.ReadAllText(readmePath);

        text.Should().Contain("## Multi-Result (All Providers)");
        text.Should().Contain("### SQL Server (multiple result sets)");
        text.Should().Contain("### PostgreSQL (refcursor outputs)");
        text.Should().Contain("### Oracle (RefCursor outputs)");
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

