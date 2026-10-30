namespace CL.WebLogic.Localization.Services;

/// <summary>
/// App-supplied texts per culture. The localizer asks for one exact culture at a time and walks
/// the fallback chain itself (<c>sv-SE</c> → <c>sv</c> → default culture, see
/// <see cref="WebCultureFallback"/>), so an implementation only answers what it has.
/// The default reads <c>{theme}/locales/{culture}.json</c> (<see cref="ThemeJsonTextResources"/>);
/// replace it with <c>web.UseTextResources&lt;T&gt;()</c> to load texts from a database or elsewhere.
/// Called on the render path: keep lookups in memory.
/// </summary>
public interface IWebTextResources
{
    /// <summary>The text for <paramref name="key"/> in exactly <paramref name="culture"/> (e.g. <c>sv-SE</c>), or null when it has none.</summary>
    string? GetText(string culture, string key);
}
