using System;
using System.IO;
using FluentAssertions;
using Xunit;

namespace AdoAsync.Tests;

public class ReadmeDocumentationTests
{
    [Fact]
    public void Readme_IncludesReadPatternsSection()
    {
        var root = FindRepoRoot(AppContext.BaseDirectory);
        var readmePath = Path.Combine(root, "src", "AdoAsync", "README.md");

        File.Exists(readmePath).Should().BeTrue($"README should exist at {readmePath}");
        var text = File.ReadAllText(readmePath);

        text.Should().Contain("## Read Patterns (3 ways)");
        text.Should().Contain("### 1) Simple Reader (ADO.NET)");
        text.Should().Contain("### 2) Streaming (AdoAsync)");
        text.Should().Contain("### 3) DataTable (AdoAsync)");
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

