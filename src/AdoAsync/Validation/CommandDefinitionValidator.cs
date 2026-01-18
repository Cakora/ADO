using FluentValidation;

namespace AdoAsync.Validation;

/// <summary>Validates command definitions.</summary>
public sealed class CommandDefinitionValidator : AbstractValidator<CommandDefinition>
{
    #region Constructors
    /// <summary>Creates the validator with required rules.</summary>
    public CommandDefinitionValidator()
    {
        RuleFor(x => x.CommandText)
            .Must(IsNonWhitespace)
            .WithMessage("CommandText must not be empty or whitespace.");
        // Stored procedures should be parameterized and validated explicitly.
        RuleFor(x => x.Parameters).NotNull().When(x => x.CommandType == System.Data.CommandType.StoredProcedure);
        RuleFor(x => x.AllowedStoredProcedures)
            .NotNull()
            .When(x => x.CommandType == System.Data.CommandType.StoredProcedure)
            .WithMessage("Stored procedures must be validated against an allow-list.");

        RuleFor(x => x)
            .Must(EnsureStoredProcedureAllowed)
            .When(x => x.CommandType == System.Data.CommandType.StoredProcedure)
            .WithMessage("Stored procedure is not in the allowed list.");

        RuleFor(x => x)
            .Must(EnsureIdentifiersAllowed)
            .When(x => x.IdentifiersToValidate is { Count: > 0 })
            .WithMessage("One or more identifiers are not in the allowed list.");
    }
    #endregion

    #region Private Helpers
    private static bool IsNonWhitespace(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        // Span-based scan avoids allocation from trimming or splitting.
        ReadOnlySpan<char> span = value.AsSpan();
        for (var i = 0; i < span.Length; i++)
        {
            if (!char.IsWhiteSpace(span[i]))
            {
                return true;
            }
        }

        return false;
    }

    private static bool EnsureStoredProcedureAllowed(CommandDefinition definition)
    {
        if (definition.CommandType != System.Data.CommandType.StoredProcedure)
        {
            return true;
        }

        if (definition.AllowedStoredProcedures is null)
        {
            // Missing allow-list means we cannot safely validate dynamic procedure names.
            return false;
        }

        try
        {
            IdentifierWhitelist.EnsureStoredProcedureAllowed(definition.CommandText, definition.AllowedStoredProcedures);
            return true;
        }
        catch (DatabaseException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private static bool EnsureIdentifiersAllowed(CommandDefinition definition)
    {
        if (definition.IdentifiersToValidate is null || definition.IdentifiersToValidate.Count == 0)
        {
            return true;
        }

        if (definition.AllowedIdentifiers is null)
        {
            // Missing allow-list means dynamic identifiers cannot be trusted.
            return false;
        }

        try
        {
            IdentifierWhitelist.EnsureIdentifiersAllowed(definition.IdentifiersToValidate, definition.AllowedIdentifiers);
            return true;
        }
        catch (DatabaseException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }
    #endregion
}
