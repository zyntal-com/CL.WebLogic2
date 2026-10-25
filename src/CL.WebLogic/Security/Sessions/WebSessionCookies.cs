using CL.WebLogic.Configuration.Models;
using CL.WebLogic.Configuration.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace CL.WebLogic.Security.Sessions;

/// <summary>Cookie names and options for sessions and the CSRF pre-session cookie.</summary>
public static class WebSessionCookies
{
    private const string HostPrefix = "__Host-";

    /// <summary>
    /// True when the site is reached over HTTPS: the request itself is HTTPS, or (outside
    /// Development) <c>PublicBaseUrl</c> is https — a TLS-terminating proxy whose headers are not
    /// trusted makes requests look like HTTP, and cookies must not silently lose <c>Secure</c> and
    /// <c>__Host-</c>. Development keeps plain-HTTP local runs working.
    /// </summary>
    public static bool IsSecure(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        if (httpContext.Request.IsHttps)
            return true;
        var services = httpContext.RequestServices;
        if (services is null || services.GetService<IHostEnvironment>() is not { } environment || environment.IsDevelopment())
            return false;
        var publicUrl = services.GetService<WebLogicConfigHolder>()?.Current.PublicBaseUrl;
        return publicUrl is not null && publicUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The cookie name to use for this request: as configured when <see cref="IsSecure"/>; without
    /// the <c>__Host-</c> prefix over plain HTTP, where browsers would reject a prefixed cookie.
    /// </summary>
    public static string Name(HttpContext httpContext, string configuredName) =>
        !IsSecure(httpContext) && configuredName.StartsWith(HostPrefix, StringComparison.Ordinal)
            ? configuredName.Substring(HostPrefix.Length)
            : configuredName;

    /// <summary>HttpOnly, SameSite=Lax, path /, Secure when <see cref="IsSecure"/>, no Domain.</summary>
    public static CookieOptions Options(HttpContext httpContext, DateTimeOffset? expires) => new()
    {
        HttpOnly = true,
        Secure = IsSecure(httpContext),
        SameSite = SameSiteMode.Lax,
        Path = "/",
        Expires = expires,
        IsEssential = true
    };

    /// <summary>The session cookie name for this request.</summary>
    public static string SessionName(HttpContext httpContext, SessionConfig config) => Name(httpContext, config.CookieName);
}
