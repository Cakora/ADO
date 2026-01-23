using System.Collections.Generic;
using System.Data.Common;
using System.Data;
using System.Linq;
using AdoAsync.Execution;

namespace AdoAsync.Helpers;

internal static class ParameterHelper
{
    internal static bool HasNonRefCursorOutputs(IReadOnlyList<DbParameter>? parameters) =>
        parameters is not null
        && parameters.Count != 0
        && parameters.Any(p =>
            p.Direction is ParameterDirection.Output or ParameterDirection.InputOutput
            && p.DataType != DbDataType.RefCursor);

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

        // Build a case-insensitive map of declared parameter definitions for normalization decisions.
        // Use a single dictionary to avoid extra allocations (ToDictionary + FrozenDictionary).
        var parameterLookup = new Dictionary<string, DbParameter>(parameters.Count, StringComparer.OrdinalIgnoreCase);
        foreach (var declared in parameters)
        {
            parameterLookup[TrimParameterPrefix(declared.Name)] = declared;
        }

        Dictionary<string, object?>? outputValues = null;

        foreach (System.Data.Common.DbParameter parameter in command.Parameters)
        {
            // Only expose OUTPUT / INPUTOUTPUT parameters (exclude ReturnValue and Input).
            if (parameter.Direction is ParameterDirection.Input or ParameterDirection.ReturnValue)
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

            outputValues ??= new Dictionary<string, object?>(command.Parameters.Count, StringComparer.OrdinalIgnoreCase);
            if (hasDefinition && definition != null)
            {
                outputValues[name] = DbValueNormalizer.Normalize(parameter.Value, definition.DataType);
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
