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

        RuleFor(x => x)
            .Must(EnsureIdentifiersAllowed)
            .When(x => x.IdentifiersToValidate is { Count: > 0 })
            // Identifier allow-lists are opt-in; we only enforce when the caller supplies identifiers.
            .WithMessage("One or more identifiers are not in the allowed list.");
    }
    #endregion

    #region Private Helpers
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
