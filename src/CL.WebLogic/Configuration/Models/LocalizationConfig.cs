using System.Globalization;
using CodeLogic.Core.Configuration;

namespace CL.WebLogic.Configuration.Models;

/// <summary>
/// Request cultures and localized texts. Off by default: with <see cref="Enabled"/> false nothing
/// is resolved, no header is added and <c>{t:key}</c> returns the key, exactly as before.
/// </summary>
public sealed class LocalizationConfig
{
    /// <summary>The culture sources <see cref="Sources"/> may list.</summary>
    public static IReadOnlyList<string> KnownSources { get; } = ["query", "cookie", "header"];

    /// <summary>Resolve a culture per request and localize <c>{t:key}</c> and form messages.</summary>
    [ConfigField(Label = "Enabled", Description = "Resolve a culture per request and localize templates and form messages.",
        Group = "Localization", Order = 1, RequiresRestart = true)]
    public bool Enabled { get; set; }

    /// <summary>Culture used when no source names a supported one. Always supported.</summary>
    [ConfigField(Label = "Default Culture", Description = "Used when no source names a supported culture, and the last text fallback.",
        Placeholder = "en", Group = "Localization", Order = 2)]
    public string DefaultCulture { get; set; } = "en";

    /// <summary>Cultures a request may select (names such as <c>sv-SE</c> or <c>sv</c>). Nothing else is ever accepted.</summary>
    [ConfigField(Label = "Supported Cultures", Description = "The only cultures a request may select (besides the default).",
        Group = "Localization", Order = 3)]
    public List<string> SupportedCultures { get; set; } = [];

    /// <summary>Where the culture is read from, in order: <c>query</c>, <c>cookie</c>, <c>header</c> (Accept-Language).</summary>
    [ConfigField(Label = "Sources", Description = "Checked in order: query, cookie, header (Accept-Language).",
        Group = "Localization", Order = 4)]
    public List<string> Sources { get; set; } = ["query", "cookie", "header"];

    /// <summary>Query-string parameter of the <c>query</c> source and of the switch endpoint.</summary>
    [ConfigField(Label = "Query Parameter", Group = "Localization", Order = 5, Collapsed = true)]
    public string QueryParameter { get; set; } = "culture";

    /// <summary>Cookie of the <c>cookie</c> source (the <c>__Host-</c> prefix is dropped over plain HTTP).</summary>
    [ConfigField(Label = "Cookie Name", Group = "Localization", Order = 6, Collapsed = true, RequiresRestart = true)]
    public string CookieName { get; set; } = "__Host-wl_culture";

    /// <summary>Lifetime of the culture cookie.</summary>
    [ConfigField(Label = "Cookie Days", Min = 1, Max = 3650, Group = "Localization", Order = 7, Collapsed = true)]
    public int CookieDays { get; set; } = 365;

    /// <summary>Path of the endpoint that sets the culture cookie (<c>GET ?culture=sv&amp;returnUrl=/</c>).</summary>
    [ConfigField(Label = "Switch Path", Group = "Localization", Order = 8, Collapsed = true, RequiresRestart = true)]
    public string SwitchPath { get; set; } = "/_weblogic/culture";

    /// <summary>Folder inside the theme with <c>{culture}.json</c> text files (built-in text resources).</summary>
    [ConfigField(Label = "Resources Folder", Description = "Theme folder with {culture}.json text files.",
        Group = "Localization", Order = 9, Collapsed = true)]
    public string ResourcesFolder { get; set; } = "locales";

    /// <summary>Also set <see cref="CultureInfo.CurrentCulture"/> (number and date formatting); the UI culture is always set.</summary>
    [ConfigField(Label = "Set Formatting Culture", Description = "Also format numbers and dates in the request culture.",
        Group = "Localization", Order = 10, Collapsed = true)]
    public bool SetCurrentCulture { get; set; } = true;

    /// <summary>The validation errors of this section (called by <see cref="WebLogicConfig.Validate"/>).</summary>
    internal IEnumerable<string> Validate()
    {
        foreach (var source in Sources)
        {
            if (!KnownSources.Contains(source, StringComparer.OrdinalIgnoreCase))
                yield return $"Localization.Sources: '{source}' must be query, cookie or header";
        }

        if (!IsToken(QueryParameter))
            yield return "Localization.QueryParameter may only contain letters, digits, '-', '_' and '.'";
        if (!IsToken(CookieName))
            yield return "Localization.CookieName may only contain letters, digits, '-', '_' and '.'";
        if (string.IsNullOrWhiteSpace(SwitchPath) || !SwitchPath.StartsWith('/') || SwitchPath.StartsWith("//", StringComparison.Ordinal))
            yield return "Localization.SwitchPath must be a local path";
        if (string.IsNullOrWhiteSpace(ResourcesFolder) || ResourcesFolder.Contains("..", StringComparison.Ordinal) || Path.IsPathRooted(ResourcesFolder))
            yield return "Localization.ResourcesFolder must be a folder inside the theme";

        if (!Enabled)
            yield break;

        if (SupportedCultures.Count > 200)
            yield return "Localization.SupportedCultures may list at most 200 cultures";
        foreach (var name in SupportedCultures.Prepend(DefaultCulture))
        {
            if (!IsKnownCulture(name))
                yield return $"Localization: '{name}' is not a known culture name";
        }
    }

    private static bool IsToken(string? value) =>
        !string.IsNullOrEmpty(value) && value.Length <= 64 && value.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.');

    private static bool IsKnownCulture(string? name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 64 || !name.All(c => char.IsAsciiLetterOrDigit(c) || c == '-'))
            return false;
        try
        {
            return CultureInfo.GetCultureInfo(name, predefinedOnly: true).Name.Length > 0;
        }
        catch (CultureNotFoundException)
        {
            return false;
        }
    }
}
