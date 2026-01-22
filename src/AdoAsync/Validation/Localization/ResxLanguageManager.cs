using System.Globalization;
using System.Resources;
using FluentValidation.Resources;

namespace AdoAsync.Validation.Localization;

/// <summary>
/// Language manager that optionally pulls messages from a ResourceManager and falls back to FluentValidation defaults.
/// </summary>
public sealed class ResxLanguageManager : LanguageManager
{
    private readonly ResourceManager? _resources;

    /// <summary>Create a language manager that optionally uses a ResourceManager.</summary>
    /// <param name="resources">Optional resource manager containing localized messages.</param>
    public ResxLanguageManager(ResourceManager? resources = null)
    {
        _resources = resources;
        Enabled = true;
    }

    /// <summary>Resolve a localized string by key, falling back to FluentValidation defaults.</summary>
    public override string GetString(string key, CultureInfo? culture = null)
    {
        var actualCulture = culture ?? CultureInfo.CurrentUICulture;
        if (_resources != null)
        {
            var text = _resources.GetString(key, actualCulture);
            if (!string.IsNullOrEmpty(text))
            {
                return text!;
            }
        }

        return base.GetString(key, actualCulture);
    }
}
