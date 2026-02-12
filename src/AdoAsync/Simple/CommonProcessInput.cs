namespace AdoAsync.Simple;

/// <summary>Common inputs for simple provider calls.</summary>
public sealed class CommonProcessInput
{
    /// <summary>Connection string for the provider.</summary>
    public string? ConnectionString { get; set; }

    /// <summary>Optional command timeout in seconds.</summary>
    public int? CommandTimeoutSeconds { get; set; }
}
