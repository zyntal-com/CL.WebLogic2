using System.Globalization;
using Microsoft.AspNetCore.Http;

namespace CL.WebLogic.Localization.Models;

/// <summary>
/// The culture resolved for a request when localization is enabled. Stored as an HttpContext
/// feature; read it with <see cref="Get"/> or <c>request.Culture</c>. Absent (null) when
/// localization is off.
/// </summary>
public sealed class WebRequestCulture
{
    /// <summary>Creates the value.</summary>
    public WebRequestCulture(CultureInfo culture, string source)
    {
        Culture = culture ?? throw new ArgumentNullException(nameof(culture));
        ArgumentException.ThrowIfNullOrEmpty(source);
        Source = source;
    }

    /// <summary>The culture: always one of <c>Localization.SupportedCultures</c> or the default.</summary>
    public CultureInfo Culture { get; }

    /// <summary>The culture name, e.g. <c>sv-SE</c>.</summary>
    public string Name => Culture.Name;

    /// <summary>Where it came from: <c>query</c>, <c>cookie</c>, <c>header</c> or <c>default</c> (see <see cref="WebCultureSources"/>).</summary>
    public string Source { get; }

    /// <summary>The culture resolved for <paramref name="httpContext"/>, or null when localization is off.</summary>
    public static WebRequestCulture? Get(HttpContext? httpContext) => httpContext?.Features.Get<WebRequestCulture>();

    /// <summary>Stores <paramref name="culture"/> on <paramref name="httpContext"/>.</summary>
    internal static void Set(HttpContext httpContext, WebRequestCulture culture) => httpContext.Features.Set(culture);
}
