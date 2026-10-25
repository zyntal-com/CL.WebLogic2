using System.Globalization;

namespace CL.WebLogic.Localization.Services;

/// <summary>Looks up localized texts for templates (<c>{t:key}</c>) and form messages.</summary>
public interface IWebTextLocalizer
{
    /// <summary>The text for <paramref name="key"/> in <paramref name="culture"/> (current UI culture when null); the key itself when missing.</summary>
    string GetText(string key, CultureInfo? culture = null);
}
