using System;
using System.Collections.Generic;
using System.Collections.Frozen;
using AdoAsync.Abstractions;
using AdoAsync.Execution;

namespace AdoAsync.DependencyInjection;

internal sealed class DbExecutorFactory : IDbExecutorFactory
{
    private readonly IReadOnlyDictionary<string, DbOptions> _optionsByName;

    public DbExecutorFactory(IEnumerable<NamedDbOptions> namedOptions)
    {
        var map = new Dictionary<string, DbOptions>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in namedOptions)
        {
            if (string.IsNullOrWhiteSpace(entry.Name))
            {
                throw new ArgumentException("Named database entry requires a non-empty Name.", nameof(namedOptions));
            }

            var key = entry.Name.Trim();
            if (map.ContainsKey(key))
            {
                throw new ArgumentException($"Duplicate database name '{key}'. Names are case-insensitive.", nameof(namedOptions));
            }

            map.Add(key, entry.Options);
        }

        _optionsByName = map.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
    }

    public IDbExecutor Create(string name, bool isInUserTransaction = false)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Database name is required.", nameof(name));
        }

        if (!_optionsByName.TryGetValue(name.Trim(), out var options))
        {
            throw new KeyNotFoundException($"No DbOptions registered for name '{name}'.");
        }

        return Create(options, isInUserTransaction);
    }

    public IDbExecutor Create<TName>(TName name, bool isInUserTransaction = false) where TName : struct, Enum
    {
        return Create(name.ToString(), isInUserTransaction);
    }

    public IDbExecutor Create(DbOptions options, bool isInUserTransaction = false)
    {
        return DbExecutor.Create(options, isInUserTransaction);
    }
}
