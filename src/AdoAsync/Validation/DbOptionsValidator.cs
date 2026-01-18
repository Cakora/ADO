using FluentValidation;

namespace AdoAsync.Validation;

/// <summary>Validates database options.</summary>
public sealed class DbOptionsValidator : AbstractValidator<DbOptions>
{
    #region Constructors
    /// <summary>Creates the validator with required rules.</summary>
    public DbOptionsValidator()
    {
        RuleFor(x => x.ConnectionString)
            .NotEmpty()
            .When(x => x.DataSource is null)
            .WithMessage("ConnectionString is required when DataSource is not provided.");
        RuleFor(x => x.CommandTimeoutSeconds).GreaterThan(0);
        RuleFor(x => x.RetryCount).GreaterThanOrEqualTo(0);
        RuleFor(x => x.RetryDelayMilliseconds).GreaterThanOrEqualTo(0);
    }
    #endregion
}
