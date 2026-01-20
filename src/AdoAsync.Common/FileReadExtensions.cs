using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Frozen;
using System.Collections.Concurrent;

namespace AdoAsync.Common;

/// <summary>Helpers for streaming and safe file reads.</summary>
public static class FileReadExtensions
{
    private static readonly ConcurrentDictionary<string, FrozenSet<string>> AssemblyResourceCache = new();

    /// <summary>Read embedded text resource synchronously; returns null when not found.</summary>
    /// <param name="assembly">Assembly containing the resource.</param>
    /// <param name="resourceName">Full resource name (e.g., Namespace.Folder.File.ext).</param>
    public static string? ReadEmbeddedText(
        Assembly assembly,
        string resourceName)
    {
        if (assembly is null)
        {
            throw new ArgumentNullException(nameof(assembly));
        }

        if (string.IsNullOrWhiteSpace(resourceName))
        {
            return null;
        }

        var stream = GetResourceStream(assembly, resourceName);
        if (stream is null)
        {
            return null;
        }

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    /// <summary>Stream lines from a file synchronously; skips missing/empty paths.</summary>
    public static IEnumerable<string> ReadLinesSafe(string? path, bool skipEmpty = true)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            yield break;
        }

        using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 4096,
            options: FileOptions.SequentialScan);

        using var reader = new StreamReader(stream);
        while (!reader.EndOfStream)
        {
            var line = reader.ReadLine();
            if (line is null)
            {
                continue;
            }

            if (skipEmpty && string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            yield return line;
        }
    }

    /// <summary>Stream lines from a file with minimal memory usage; skips missing/empty paths.</summary>
    public static async IAsyncEnumerable<string> ReadLinesSafeAsync(
        string? path,
        bool skipEmpty = true,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            yield break;
        }

        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 4096,
            options: FileOptions.Asynchronous | FileOptions.SequentialScan);

        using var reader = new StreamReader(stream);
        while (!reader.EndOfStream)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var line = await reader.ReadLineAsync().ConfigureAwait(false);
            if (line is null)
            {
                continue;
            }

            if (skipEmpty && string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            yield return line;
        }
    }

    /// <summary>Read entire file text; returns null for missing/empty path.</summary>
    public static async Task<string?> ReadAllTextSafeAsync(string? path, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return null;
        }

        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 4096,
            options: FileOptions.Asynchronous | FileOptions.SequentialScan);

        using var reader = new StreamReader(stream);
        cancellationToken.ThrowIfCancellationRequested();
        return await reader.ReadToEndAsync().ConfigureAwait(false);
    }

    /// <summary>Read entire file text synchronously; returns null for missing/empty path.</summary>
    public static string? ReadAllTextSafe(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return null;
        }

        using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 4096,
            options: FileOptions.SequentialScan);

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    /// <summary>Read embedded resource text by name; returns null when not found.</summary>
    public static async Task<string?> ReadEmbeddedTextAsync(
        Assembly assembly,
        string resourceName,
        CancellationToken cancellationToken = default)
    {
        if (assembly is null)
        {
            throw new ArgumentNullException(nameof(assembly));
        }

        if (string.IsNullOrWhiteSpace(resourceName))
        {
            return null;
        }

        var stream = GetResourceStream(assembly, resourceName);
        if (stream is null)
        {
            return null;
        }

        using var reader = new StreamReader(stream);
        cancellationToken.ThrowIfCancellationRequested();
        return await reader.ReadToEndAsync().ConfigureAwait(false);
    }

    private static Stream? GetResourceStream(Assembly assembly, string resourceName)
    {
        // Cache resource names per assembly to avoid repeated lookups; FrozenSet for low overhead.
        var names = AssemblyResourceCache.GetOrAdd(
            assembly.FullName ?? assembly.GetHashCode().ToString(),
            _ => assembly.GetManifestResourceNames().ToFrozenSet(StringComparer.Ordinal));

        if (!names.Contains(resourceName))
        {
            return null;
        }

        return assembly.GetManifestResourceStream(resourceName);
    }
}
