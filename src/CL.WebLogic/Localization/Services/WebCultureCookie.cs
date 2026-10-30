using CL.WebLogic.Configuration.Services;
using CL.WebLogic.Security.Sessions;
using Microsoft.AspNetCore.Http;

namespace CL.WebLogic.Localization.Services;

/// <summary>
/// Writes the culture cookie read by the <c>cookie</c> source. Used by the switch endpoint
/// (<c>Localization.SwitchPath</c>); call it from your own handlers to switch from a form or a
/// profile setting. HttpOnly, SameSite=Lax, path <c>/</c>, Secure when the site is reached over HTTPS.
/// </summary>
public sealed class WebCultureCookie
{
    private readonly WebLogicConfigHolder _config;
    private readonly WebCultureResolver _resolver;
    private readonly TimeProvider _time;

    /// <summary>Creates the service.</summary>
    public WebCultureCookie(WebLogicConfigHolder config, WebCultureResolver resolver, TimeProvider? time = null)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
        _time = time ?? TimeProvider.System;
    }

    /// <summary>
    /// Sets the cookie to the supported culture matching <paramref name="culture"/>. False (and
    /// nothing written) when localization is off or the culture is not supported.
    /// </summary>
    public bool TrySet(HttpContext httpContext, string? culture)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        var config = _config.Current.Localization;
        if (!config.Enabled || !_resolver.TryMatch(culture, out var match))
            return false;

        var options = WebSessionCookies.Options(httpContext, _time.GetUtcNow().AddDays(config.CookieDays));
        httpContext.Response.Cookies.Append(WebSessionCookies.Name(httpContext, config.CookieName), match.Name, options);
        return true;
    }

    /// <summary>Removes the cookie, so the next requests use the other sources again.</summary>
    public void Clear(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        var config = _config.Current.Localization;
        httpContext.Response.Cookies.Delete(WebSessionCookies.Name(httpContext, config.CookieName), WebSessionCookies.Options(httpContext, null));
    }
}
