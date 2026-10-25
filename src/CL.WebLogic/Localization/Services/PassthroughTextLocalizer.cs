using System.Globalization;

namespace CL.WebLogic.Localization.Services;

/// <summary>Default localizer: returns the key unchanged. Replace it to localize templates.</summary>
public sealed class PassthroughTextLocalizer : IWebTextLocalizer
{
    /// <inheritdoc/>
    public string GetText(string key, CultureInfo? culture = null) => key;
}
