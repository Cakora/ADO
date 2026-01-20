namespace AdoAsync.DependencyInjection;

/// <summary>Named database options for multi-database registration.</summary>
public sealed record NamedDbOptions(string Name, DbOptions Options);

