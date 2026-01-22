using System.Collections.Generic;
using System.Collections.Frozen;
using System.Data.Common;
using System.Data;
using System.Linq;
using AdoAsync.Execution;

namespace AdoAsync.Helpers;

internal static class ParameterHelper
{
    /// <summary>Extract non-input parameters, skipping refcursors (handled as result sets) and normalizing by declared DbDataType.</summary>
    /// <param name="command">Executed command containing provider parameters.</param>
    /// <param name="parameters">Caller-declared parameters with DbDataType metadata.</param>
    /// <returns>Output parameter values keyed by name.</returns>
    public static IReadOnlyDictionary<string, object?>? ExtractOutputParameters(DbCommand command, IReadOnlyList<DbParameter>? parameters)
    {
        if (parameters is null || parameters.Count == 0 || command.Parameters.Count == 0)
        {
            return null;
        }

        // Freeze the declared parameter map for fast, repeated lookups when extracting outputs.
        var parameterLookup = parameters
            .ToDictionary(p => TrimParameterPrefix(p.Name), StringComparer.OrdinalIgnoreCase)
            .ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

        Dictionary<string, object?>? outputValues = null;

        foreach (System.Data.Common.DbParameter parameter in command.Parameters)
        {
            if (parameter.Direction == ParameterDirection.Input)
            {
                continue;
            }

            var name = TrimParameterPrefix(parameter.ParameterName);
            DbParameter? definition = null;
            var hasDefinition = parameterLookup.TryGetValue(name, out definition);

            if (hasDefinition && definition != null && definition.DataType == DbDataType.RefCursor)
            {
                continue;
            }

            outputValues ??= new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            if (hasDefinition && definition != null)
            {
                outputValues[name] = OutputParameterConverter.Normalize(parameter.Value, definition.DataType);
            }
            else
            {
                outputValues[name] = parameter.Value is DBNull ? null : parameter.Value;
            }
        }

        return outputValues;
    }

    /// <summary>Remove provider parameter prefix characters.</summary>
    /// <param name="name">Parameter name possibly containing a prefix.</param>
    /// <returns>Name without provider prefix.</returns>
    public static string TrimParameterPrefix(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return name;
        }

        return name[0] is '@' or ':' or '?' ? name[1..] : name;
    }
}
