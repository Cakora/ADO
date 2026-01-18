using System;
using System.Collections.Generic;
using System.Linq;
using FluentValidation;

namespace AdoAsync.Validation;

/// <summary>Validates bulk import requests.</summary>
public sealed class BulkImportRequestValidator : AbstractValidator<BulkImportRequest>
{
    #region Constructors
    /// <summary>Creates the validator with required rules.</summary>
    public BulkImportRequestValidator()
    {
        RuleFor(x => x.DestinationTable).NotEmpty();
        RuleFor(x => x.SourceReader).NotNull();
        RuleFor(x => x.ColumnMappings).NotEmpty();

        RuleFor(x => x)
            .Must(HasUniqueDestinationColumns)
            .WithMessage("Destination columns must be unique.");

        RuleFor(x => x)
            .Must(HasUniqueSourceColumns)
            .WithMessage("Source columns must be unique.");

        RuleFor(x => x.AllowedDestinationTables)
            .NotNull()
            // Bulk import targets should be explicitly allow-listed to avoid accidental writes.
            .WithMessage("Destination tables must be validated against an allow-list.");

        RuleFor(x => x)
            .Must(EnsureDestinationTableAllowed)
            .WithMessage("Destination table is not in the allowed list.");

        RuleFor(x => x)
            .Must(EnsureDestinationColumnsAllowed)
            .When(x => x.ColumnMappings is { Count: > 0 })
            .WithMessage("One or more destination columns are not in the allowed list.");
    }
    #endregion

    #region Private Helpers
    private static bool HasUniqueDestinationColumns(BulkImportRequest request)
    {
        return request.ColumnMappings
            .Select(mapping => mapping.DestinationColumn)
            .Distinct(StringComparer.Ordinal)
            .Count() == request.ColumnMappings.Count;
    }

    private static bool HasUniqueSourceColumns(BulkImportRequest request)
    {
        return request.ColumnMappings
            .Select(mapping => mapping.SourceColumn)
            .Distinct(StringComparer.Ordinal)
            .Count() == request.ColumnMappings.Count;
    }

    private static bool EnsureDestinationTableAllowed(BulkImportRequest request)
    {
        if (request.AllowedDestinationTables is null)
        {
            return false;
        }

        try
        {
            IdentifierWhitelist.EnsureIdentifierAllowed(request.DestinationTable, request.AllowedDestinationTables);
            return true;
        }
        catch (DatabaseException)
        {
            return false;
        }
    }

    private static bool EnsureDestinationColumnsAllowed(BulkImportRequest request)
    {
        if (request.AllowedDestinationColumns is null)
        {
            return false;
        }

        var destinationColumns = request.ColumnMappings.Select(mapping => mapping.DestinationColumn);

        try
        {
            IdentifierWhitelist.EnsureIdentifiersAllowed(destinationColumns, request.AllowedDestinationColumns);
            return true;
        }
        catch (DatabaseException)
        {
            return false;
        }
    }
    #endregion
}
