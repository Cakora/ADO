using System;
using System.Collections.Generic;
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
        var messages = new List<string>();
        foreach (var failure in failures)
        {
            messages.Add($"{failure.PropertyName}: {failure.ErrorMessage}");
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
