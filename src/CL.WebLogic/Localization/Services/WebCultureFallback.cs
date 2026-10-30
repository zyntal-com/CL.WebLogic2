namespace CL.WebLogic.Localization.Services;

/// <summary>The order in which cultures are tried for a text.</summary>
public static class WebCultureFallback
{
    /// <summary>
    /// <paramref name="culture"/> and its parents by name, then <paramref name="defaultCulture"/> and
    /// its parents, without duplicates: <c>sv-SE</c> with default <c>en-US</c> gives
    /// <c>sv-SE, sv, en-US, en</c>. Parents are found by dropping the last <c>-</c> subtag.
    /// </summary>
    public static IReadOnlyList<string> Chain(string? culture, string? defaultCulture)
    {
        var chain = new List<string>(4);
        Add(chain, culture);
        Add(chain, defaultCulture);
        return chain;
    }

    private static void Add(List<string> chain, string? name)
    {
        while (!string.IsNullOrEmpty(name))
        {
            if (!chain.Contains(name, StringComparer.OrdinalIgnoreCase))
                chain.Add(name);
            var dash = name.LastIndexOf('-');
            name = dash > 0 ? name[..dash] : null;
        }
    }
}
