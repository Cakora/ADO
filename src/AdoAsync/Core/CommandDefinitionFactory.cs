using System.Collections.Generic;
using System.Data;
using AdoAsync.Helpers;

namespace AdoAsync;

/// <summary>Factory helpers to reduce CommandDefinition repetition.</summary>
public static class CommandDefinitionFactory
{
    /// <summary>Parameter specification used to build DbParameter instances.</summary>
    public sealed record DbParameterSpec(
        string Name,
        DbDataType DataType,
        ParameterDirection Direction,
        object? Value = null,
        int? Size = null,
        byte? Precision = null,
        byte? Scale = null,
        string? StructuredTypeName = null,
        bool IsArrayBinding = false);

    /// <summary>Create a CommandDefinition from parameter specifications.</summary>
    public static CommandDefinition Create(
        string commandText,
        CommandType commandType,
        IEnumerable<DbParameterSpec>? parameterSpecs = null,
        DatabaseType? databaseType = null,
        int? commandTimeoutSeconds = null,
        CommandBehavior behavior = CommandBehavior.Default,
        IReadOnlySet<string>? allowedIdentifiers = null,
        IReadOnlyList<string>? identifiersToValidate = null)
    {
        var parameters = parameterSpecs is null ? null : CreateParameters(parameterSpecs, databaseType);
        return new CommandDefinition
        {
            CommandText = commandText,
            CommandType = commandType,
            Parameters = parameters,
            CommandTimeoutSeconds = commandTimeoutSeconds,
            Behavior = behavior,
            AllowedIdentifiers = allowedIdentifiers,
            IdentifiersToValidate = identifiersToValidate
        };
    }

    private static IReadOnlyList<DbParameter>? CreateParameters(IEnumerable<DbParameterSpec> specs, DatabaseType? databaseType)
    {
        var list = specs is ICollection<DbParameterSpec> collection
            ? new List<DbParameter>(collection.Count)
            : new List<DbParameter>();

        foreach (var spec in specs)
        {
            var name = databaseType.HasValue ? NormalizeParameterName(databaseType.Value, spec.Name) : spec.Name;
            list.Add(new DbParameter
            {
                Name = name,
                DataType = spec.DataType,
                Direction = spec.Direction,
                Value = spec.Value,
                Size = spec.Size,
                Precision = spec.Precision,
                Scale = spec.Scale,
                StructuredTypeName = spec.StructuredTypeName,
                IsArrayBinding = spec.IsArrayBinding
            });
        }

        return list.Count == 0 ? null : list.ToArray();
    }

    private static string NormalizeParameterName(DatabaseType databaseType, string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return name;
        }

        return databaseType switch
        {
            DatabaseType.SqlServer => "@" + ParameterHelper.TrimParameterPrefix(name),
            DatabaseType.PostgreSql => ParameterHelper.TrimParameterPrefix(name),
            DatabaseType.Oracle => ParameterHelper.TrimParameterPrefix(name),
            _ => name
        };
    }
}
