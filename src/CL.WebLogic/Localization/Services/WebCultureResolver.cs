using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using CL.WebLogic.Configuration.Models;
using CL.WebLogic.Configuration.Services;
using CL.WebLogic.Localization.Models;
using CL.WebLogic.Security.Sessions;
using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;

namespace CL.WebLogic.Localization.Services;

/// <summary>
/// Resolves the request culture from <c>Localization.Sources</c> in order (query, cookie,
/// Accept-Language), falling back to <c>Localization.DefaultCulture</c>. Only the default and the
/// <c>SupportedCultures</c> are ever returned: input is compared with the configured names and
/// never turned into a <see cref="CultureInfo"/> itself. Oversized or malformed values are ignored.
/// </summary>
public sealed class WebCultureResolver
{
    /// <summary>Longest candidate name considered (BCP 47 tags in practice are far shorter).</summary>
    public const int MaxCandidateLength = 64;

    private const int MaxAcceptLanguageLength = 1024;
    private const int MaxAcceptLanguageEntries = 16;

    private readonly WebLogicConfigHolder _config;

    /// <summary>Creates the resolver.</summary>
    public WebCultureResolver(WebLogicConfigHolder config) => _config = config ?? throw new ArgumentNullException(nameof(config));

    /// <summary>The culture for <paramref name="httpContext"/>, or null when localization is off.</summary>
    public WebRequestCulture? Resolve(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        var config = _config.Current.Localization;
        if (!config.Enabled)
            return null;

        var request = httpContext.Request;
        foreach (var source in config.Sources)
        {
            if (string.Equals(source, WebCultureSources.Query, StringComparison.OrdinalIgnoreCase))
            {
                if (request.Query.TryGetValue(config.QueryParameter, out var values) && values.Count > 0 && TryMatch(config, values[0], out var culture))
                    return new WebRequestCulture(culture, WebCultureSources.Query);
            }
            else if (string.Equals(source, WebCultureSources.Cookie, StringComparison.OrdinalIgnoreCase))
            {
                if (request.Cookies.TryGetValue(WebSessionCookies.Name(httpContext, config.CookieName), out var value) && TryMatch(config, value, out var culture))
                    return new WebRequestCulture(culture, WebCultureSources.Cookie);
            }
            else if (string.Equals(source, WebCultureSources.Header, StringComparison.OrdinalIgnoreCase))
            {
                foreach (var tag in AcceptLanguages(request.Headers.AcceptLanguage.ToString()))
                {
                    if (TryMatch(config, tag, out var culture))
                        return new WebRequestCulture(culture, WebCultureSources.Header);
                }
            }
        }
        return new WebRequestCulture(DefaultCulture(config), WebCultureSources.Default);
    }

    /// <summary>
    /// Matches <paramref name="candidate"/> against the supported cultures (default included):
    /// exactly, then by its parents (<c>sv-FI</c> → <c>sv</c>), then by language
    /// (<c>sv</c> → the first supported <c>sv-*</c>). Case-insensitive; <c>_</c> counts as <c>-</c>.
    /// </summary>
    public bool TryMatch(string? candidate, [NotNullWhen(true)] out CultureInfo? culture) =>
        TryMatch(_config.Current.Localization, candidate, out culture);

    private static bool TryMatch(LocalizationConfig config, string? candidate, [NotNullWhen(true)] out CultureInfo? culture)
    {
        culture = null;
        if (string.IsNullOrEmpty(candidate) || candidate.Length > MaxCandidateLength)
            return false;
        foreach (var c in candidate)
        {
            if (!char.IsAsciiLetterOrDigit(c) && c is not ('-' or '_'))
                return false;
        }

        var name = candidate.Replace('_', '-');
        for (var current = name; current.Length > 0;)
        {
            if (Find(config, s => string.Equals(s, current, StringComparison.OrdinalIgnoreCase)) is { } exact)
                return Create(exact, out culture);
            var dash = current.LastIndexOf('-');
            current = dash > 0 ? current[..dash] : string.Empty;
        }

        var language = name.Split('-')[0] + "-";
        return Find(config, s => s.StartsWith(language, StringComparison.OrdinalIgnoreCase)) is { } sameLanguage && Create(sameLanguage, out culture);
    }

    private static string? Find(LocalizationConfig config, Func<string, bool> predicate)
    {
        if (!string.IsNullOrEmpty(config.DefaultCulture) && predicate(config.DefaultCulture))
            return config.DefaultCulture;
        foreach (var supported in config.SupportedCultures)
        {
            if (!string.IsNullOrEmpty(supported) && predicate(supported))
                return supported;
        }
        return null;
    }

    private static bool Create(string configuredName, [NotNullWhen(true)] out CultureInfo? culture)
    {
        try
        {
            culture = CultureInfo.GetCultureInfo(configuredName);
            return true;
        }
        catch (CultureNotFoundException)
        {
            culture = null;
            return false;
        }
    }

    private static CultureInfo DefaultCulture(LocalizationConfig config) =>
        Create(config.DefaultCulture, out var culture) ? culture : CultureInfo.InvariantCulture;

    /// <summary>The language tags of an <c>Accept-Language</c> value, best first (at most 16; <c>*</c> and <c>q=0</c> dropped).</summary>
    internal static IEnumerable<string> AcceptLanguages(string header)
    {
        if (string.IsNullOrWhiteSpace(header))
            return [];
        if (header.Length > MaxAcceptLanguageLength)
            header = header[..MaxAcceptLanguageLength];

        var entries = new List<(string Tag, double Quality)>(4);
        foreach (var part in header.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Take(MaxAcceptLanguageEntries))
        {
            if (!StringWithQualityHeaderValue.TryParse(part, out var parsed) || !parsed.Value.HasValue)
                continue;
            var quality = parsed.Quality ?? 1.0;
            var tag = parsed.Value.Value!;
            if (quality > 0 && tag != "*")
                entries.Add((tag, quality));
        }
        return entries.OrderByDescending(e => e.Quality).Select(e => e.Tag);
    }
}
