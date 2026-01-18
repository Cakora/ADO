namespace AdoAsync;

/// <summary>
/// Centralized argument validation that throws DatabaseException.
/// </summary>
public static class Validate
{
    /// <summary>Throws a <see cref="DatabaseException"/> when a required value is null.</summary>
    public static void Required(object? value, string paramName)
    {
        if (value is null)
        {
            throw new DatabaseException(ErrorCategory.Validation, $"{paramName} is required.");
        }
    }
}
