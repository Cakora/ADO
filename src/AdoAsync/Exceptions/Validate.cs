namespace AdoAsync;

/// <summary>
/// Guard helpers for common validation scenarios.
/// </summary>
public static class Validate
{
    /// <summary>Throws when the supplied value is null.</summary>
    public static void Required(object? value, string name)
    {
        // Centralized guard to keep error messages consistent.
        if (value is null)
        {
            throw new DatabaseException(ErrorCategory.Validation, $"{name} is required.");
        }
    }
}
