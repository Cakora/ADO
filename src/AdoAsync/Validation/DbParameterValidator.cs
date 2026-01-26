using System;
using FluentValidation;

namespace AdoAsync.Validation;

/// <summary>Validates parameters for correctness and provider support.</summary>
public sealed class DbParameterValidator : AbstractValidator<DbParameter>
{
    #region Constants
    private const bool SupportsUnsignedTypes = false;
    #endregion

    #region Constructors
    /// <summary>Creates the validator with required rules.</summary>
    public DbParameterValidator()
    {
        RuleFor(x => x.Direction)
            .Must(direction => Enum.IsDefined(direction))
            .WithMessage("Parameter Direction is invalid.");

        RuleFor(x => x.DataType)
            .Must(type => Enum.IsDefined(type))
            .WithMessage("Parameter DbDataType is invalid.");

        RuleFor(x => x.Name).NotEmpty();

        RuleFor(x => x.Direction)
            .Must(direction => direction != System.Data.ParameterDirection.ReturnValue)
            .WithMessage("ReturnValue parameters are not supported.");

        RuleFor(x => x)
            .Must(p => !IsLengthConstrainedType(p.DataType) || p.Size.HasValue)
            .When(p => p.Direction is System.Data.ParameterDirection.Output
                or System.Data.ParameterDirection.InputOutput)
            // Output parameters need explicit sizing across providers.
            .WithMessage("Output parameters must specify Size.");

        RuleFor(x => x)
            .Must(p => !IsLengthConstrainedType(p.DataType) || p.Size.HasValue)
            .When(p => p.Direction == System.Data.ParameterDirection.Input)
            .WithMessage("String parameters should specify Size when length is constrained.");

        RuleFor(x => x)
            .Must(p => p.DataType != DbDataType.Decimal
                       && p.DataType != DbDataType.Currency
                       || (p.Precision.HasValue && p.Scale.HasValue))
            .WithMessage("Decimal parameters must specify Precision and Scale.");

        RuleFor(x => x)
            .Must(p => p.DataType != DbDataType.Timestamp
                       || p.Value is null
                       || p.Value is not DateTime and not DateTimeOffset)
            .WithMessage("Timestamp parameters must not use date/time values.");

        RuleFor(x => x)
            .Must(p => p.DataType != DbDataType.UInt16
                       && p.DataType != DbDataType.UInt32
                       && p.DataType != DbDataType.UInt64)
            .When(_ => !SupportsUnsignedTypes)
            // Keep unsigned disabled to avoid silent overflow across providers.
            .WithMessage("Unsigned types are not supported by the current provider.");

        RuleFor(x => x)
            .Must(p => p.DataType != DbDataType.RefCursor
                       || p.Direction is System.Data.ParameterDirection.Output
                       or System.Data.ParameterDirection.InputOutput)
            // RefCursor is a provider feature (Oracle/PostgreSQL) and only valid as an output.
            .WithMessage("RefCursor parameters must be Output or InputOutput.");

        RuleFor(x => x)
            .Must(p => p.DataType != DbDataType.Structured
                       || (p.Direction == System.Data.ParameterDirection.Input
                           && !string.IsNullOrWhiteSpace(p.StructuredTypeName)
                           && p.Value is not null))
            // TVP/structured parameters are SQL Server-specific and input-only.
            .WithMessage("Structured parameters must be Input, specify StructuredTypeName, and provide a Value.");

        RuleFor(x => x)
            .Must(p => !p.IsArrayBinding || (p.Direction == System.Data.ParameterDirection.Input && p.Value is Array a && a.Length > 0))
            // Keep array binding explicit and input-only (Oracle PLSQL associative arrays).
            .WithMessage("Array binding parameters must be Input and provide a non-empty array Value.");

        RuleFor(x => x)
            .Must(p => !p.IsArrayBinding
                       || !IsLengthConstrainedType(p.DataType)
                       || p.Size.HasValue)
            // Oracle string array binding requires an explicit per-element size.
            .WithMessage("Array binding string parameters must specify Size.");
    }
    #endregion

    #region Private Helpers
    private static bool IsLengthConstrainedType(DbDataType dataType) =>
        dataType switch
        {
            DbDataType.String => true,
            DbDataType.AnsiString => true,
            DbDataType.StringFixed => true,
            DbDataType.AnsiStringFixed => true,
            // Future: enable length checks for these types when provider behavior is standardized.
            DbDataType.Clob => false,
            DbDataType.NClob => false,
            DbDataType.Binary => false,
            DbDataType.Blob => false,
            DbDataType.Json => false,
            DbDataType.Xml => false,
            _ => false
        };
    #endregion
}
