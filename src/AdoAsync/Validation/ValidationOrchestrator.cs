using System;
using System.Buffers;
using System.Collections.Generic;
using AdoAsync;
using FluentValidation;

namespace AdoAsync.Validation;

/// <summary>
/// Runs validators when enabled and maps failures to DbError.
/// </summary>
public static class ValidationOrchestrator
{
    #region Public API
    /// <summary>Validates options when enabled.</summary>
    public static DbError? ValidateOptions(DbOptions options, bool enableValidation, IValidator<DbOptions> validator)
    {
        if (!enableValidation)
        {
            return null;
        }

        Validate.Required(options, nameof(options));
        Validate.Required(validator, nameof(validator));

        var result = validator.Validate(options);
        return result.IsValid ? null : ToError(result.Errors);
    }

    /// <summary>Validates a command and its parameters when enabled.</summary>
    public static DbError? ValidateCommand(CommandDefinition command, bool enableValidation, IValidator<CommandDefinition> validator, IValidator<DbParameter> parameterValidator)
    {
        if (!enableValidation)
        {
            return null;
        }

        Validate.Required(command, nameof(command));
        Validate.Required(validator, nameof(validator));
        Validate.Required(parameterValidator, nameof(parameterValidator));

        var result = validator.Validate(command);
        if (!result.IsValid)
        {
            return ToError(result.Errors);
        }

        if (command.Parameters is { } parameters)
        {
            // Validate parameters individually to keep error context precise.
            foreach (var param in parameters)
            {
                var paramResult = parameterValidator.Validate(param);
                if (!paramResult.IsValid)
                {
                    return ToError(paramResult.Errors);
                }
            }
        }

        return null;
    }

    /// <summary>Validates bulk import requests when enabled.</summary>
    public static DbError? ValidateBulkImport(BulkImportRequest request, bool enableValidation, IValidator<BulkImportRequest> validator)
    {
        if (!enableValidation)
        {
            return null;
        }

        Validate.Required(request, nameof(request));
        Validate.Required(validator, nameof(validator));

        var result = validator.Validate(request);
        return result.IsValid ? null : ToError(result.Errors);
    }
    #endregion

    #region Private Helpers
    private static DbError ToError(IEnumerable<FluentValidation.Results.ValidationFailure> failures)
    {
        // Preserve property context in a compact, client-friendly format.
        IReadOnlyList<string> messages;
        if (failures is ICollection<FluentValidation.Results.ValidationFailure> collection)
        {
            if (collection.Count == 0)
            {
                messages = Array.Empty<string>();
            }
            else
            {
                var rented = ArrayPool<string>.Shared.Rent(collection.Count);
                var index = 0;
                // Pooling amortizes allocations when validation emits many failures.
                foreach (var failure in collection)
                {
                    rented[index++] = $"{failure.PropertyName}: {failure.ErrorMessage}";
                }

                var result = new string[index];
                Array.Copy(rented, result, index);
                // Clear pooled references to avoid retaining messages longer than needed.
                Array.Clear(rented, 0, index);
                ArrayPool<string>.Shared.Return(rented);
                messages = result;
            }
        }
        else
        {
            var list = new List<string>();
            foreach (var failure in failures)
            {
                list.Add($"{failure.PropertyName}: {failure.ErrorMessage}");
            }

            messages = list;
        }

        return new DbError
        {
            Type = DbErrorType.ValidationError,
            Code = DbErrorCode.ValidationFailed,
            MessageKey = "errors.validation",
            MessageParameters = messages,
            IsTransient = false
        };
    }
    #endregion
}
