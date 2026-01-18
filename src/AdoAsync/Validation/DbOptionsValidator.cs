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
            // DataSource allows preconfigured connections; otherwise a raw connection string is mandatory.
            .WithMessage("ConnectionString is required when DataSource is not provided.");
        RuleFor(x => x.CommandTimeoutSeconds).GreaterThan(0);
        // Allow zero to effectively disable retrying without rejecting the config.
        RuleFor(x => x.RetryCount).GreaterThanOrEqualTo(0);
        // Delay is non-negative to avoid accidental negative time spans.
        RuleFor(x => x.RetryDelayMilliseconds).GreaterThanOrEqualTo(0);
    }
    #endregion
}
