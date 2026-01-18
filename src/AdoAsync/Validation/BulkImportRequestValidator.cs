using System;
using System.Collections.Generic;
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
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var mapping in request.ColumnMappings)
        {
            if (!seen.Add(mapping.DestinationColumn))
            {
                return false;
            }
        }

        return true;
    }

    private static bool HasUniqueSourceColumns(BulkImportRequest request)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var mapping in request.ColumnMappings)
        {
            if (!seen.Add(mapping.SourceColumn))
            {
                return false;
            }
        }

        return true;
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
        catch (InvalidOperationException)
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

        var destinationColumns = new List<string>();
        foreach (var mapping in request.ColumnMappings)
        {
            destinationColumns.Add(mapping.DestinationColumn);
        }

        try
        {
            IdentifierWhitelist.EnsureIdentifiersAllowed(destinationColumns, request.AllowedDestinationColumns);
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }
    #endregion
}
