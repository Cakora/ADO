using FluentValidation;

namespace AdoAsync.Validation;

/// <summary>Validates database options.</summary>
public sealed class DbOptionsValidator : AbstractValidator<DbOptions>
{
    #region Constructors
    /// <summary>Creates the validator with required rules.</summary>
    public DbOptionsValidator()
    {
        // All validation runs only when EnableValidation is true
        When(x => x.EnableValidation, () =>
        {
            // ConnectionString is required if DataSource is not provided
            RuleFor(x => x.ConnectionString)
                .NotEmpty()
                .When(x => x.DataSource is null)
                .WithMessage("ConnectionString is required.");

            // DataSource is required if ConnectionString is not provided
            RuleFor(x => x.DataSource)
                .NotNull()
                .When(x => string.IsNullOrWhiteSpace(x.ConnectionString))
                .WithMessage("DataSource is required.");

            // Require positive timeout to avoid infinite commands by default.
            RuleFor(x => x.CommandTimeoutSeconds)
                .GreaterThan(0)
                .WithMessage("CommandTimeoutSeconds must be a positive value.");

            // Retry validation only when EnableRetry is also true
            When(x => x.EnableRetry, () =>
            {
                RuleFor(x => x.RetryCount).GreaterThanOrEqualTo(0);
                RuleFor(x => x.RetryDelayMilliseconds).GreaterThanOrEqualTo(0);
            });
        });
    }
    #endregion
}
