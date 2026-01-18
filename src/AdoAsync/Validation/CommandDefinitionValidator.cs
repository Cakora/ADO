using FluentValidation;

namespace AdoAsync.Validation;

/// <summary>Validates command definitions.</summary>
public sealed class CommandDefinitionValidator : AbstractValidator<CommandDefinition>
{
    #region Constructors
    /// <summary>Creates the validator with required rules.</summary>
    public CommandDefinitionValidator()
    {
        RuleFor(x => x.CommandText).NotEmpty();
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
    private static bool EnsureStoredProcedureAllowed(CommandDefinition definition)
    {
        if (definition.CommandType != System.Data.CommandType.StoredProcedure)
        {
            return true;
        }

        if (definition.AllowedStoredProcedures is null)
        {
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
    }

    private static bool EnsureIdentifiersAllowed(CommandDefinition definition)
    {
        if (definition.IdentifiersToValidate is null || definition.IdentifiersToValidate.Count == 0)
        {
            return true;
        }

        if (definition.AllowedIdentifiers is null)
        {
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
    }
    #endregion
}
