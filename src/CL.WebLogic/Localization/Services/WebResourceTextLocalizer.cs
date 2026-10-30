using System.Globalization;
using CL.WebLogic.Configuration.Services;

namespace CL.WebLogic.Localization.Services;

/// <summary>
/// Default localizer. With <c>Localization.Enabled</c> false it returns the key (like
/// <see cref="PassthroughTextLocalizer"/>); when enabled it looks the key up in
/// <see cref="IWebTextResources"/> along <see cref="WebCultureFallback.Chain"/> and returns the
/// key when no culture has it.
/// </summary>
public sealed class WebResourceTextLocalizer : IWebTextLocalizer
{
    private readonly WebLogicConfigHolder _config;
    private readonly IWebTextResources _resources;

    /// <summary>Creates the localizer.</summary>
    public WebResourceTextLocalizer(WebLogicConfigHolder config, IWebTextResources resources)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _resources = resources ?? throw new ArgumentNullException(nameof(resources));
    }

    /// <inheritdoc/>
    public string GetText(string key, CultureInfo? culture = null)
    {
        var localization = _config.Current.Localization;
        if (!localization.Enabled || string.IsNullOrEmpty(key))
            return key;

        foreach (var name in WebCultureFallback.Chain((culture ?? CultureInfo.CurrentUICulture).Name, localization.DefaultCulture))
        {
            if (_resources.GetText(name, key) is { } text)
                return text;
        }
        return key;
    }
}
