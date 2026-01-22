using System.Collections.Generic;
using FluentValidation;

namespace AdoAsync.Validation;

/// <summary>
/// Runs validators when enabled and maps failures to DbError.
/// </summary>
public static class ValidationRunner
{
    static ValidationRunner()
    {
        // Default to English FluentValidation resources; callers can override globally if they register their own LanguageManager.
        ValidatorOptions.Global.LanguageManager = new Validation.Localization.ResxLanguageManager();
    }
    #region Public API
    /// <summary>Validates a value when enabled.</summary>
    public static DbError? Validate<T>(T value, bool enableValidation, IValidator<T> validator)
    {
        if (!enableValidation)
        {
            return null;
        }

        global::AdoAsync.Validate.Required(value, nameof(value));
        global::AdoAsync.Validate.Required(validator, nameof(validator));

        var result = validator.Validate(value);
        return result.IsValid ? null : ToError(result.Errors);
    }

    /// <summary>Validates options when enabled.</summary>
    public static DbError? ValidateOptions(DbOptions options, bool enableValidation, IValidator<DbOptions> validator)
    {
        return Validate(options, enableValidation, validator);
    }

    /// <summary>Validates a command and its parameters when enabled.</summary>
    public static DbError? ValidateCommand(CommandDefinition command, bool enableValidation, IValidator<CommandDefinition> validator, IValidator<DbParameter> parameterValidator)
    {
        if (!enableValidation)
        {
            return null;
        }

        global::AdoAsync.Validate.Required(command, nameof(command));
        global::AdoAsync.Validate.Required(validator, nameof(validator));
        global::AdoAsync.Validate.Required(parameterValidator, nameof(parameterValidator));

        var commandError = Validate(command, enableValidation, validator);
        if (commandError is not null) return commandError;

        if (command.Parameters is { } parameters)
        {
            foreach (var param in parameters)
            {
                var paramResult = parameterValidator.Validate(param);
                if (!paramResult.IsValid)
                {
                    // Return first parameter error to keep payload small and actionable.
                    return ToError(paramResult.Errors);
                }
            }
        }

        return null;
    }

    /// <summary>Validates bulk import requests when enabled.</summary>
    public static DbError? ValidateBulkImport(BulkImportRequest request, bool enableValidation, IValidator<BulkImportRequest> validator)
    {
        return Validate(request, enableValidation, validator);
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
