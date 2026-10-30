using CL.WebLogic.Configuration.Models;
using CL.WebLogic.Runtime.Services;
using CL.WebLogic.Security.Pipeline;
using CL.WebLogic.Security.Sessions;
using Microsoft.AspNetCore.Http;

namespace CL.WebLogic.Caching.Services;

/// <summary>
/// The single "could this response hold something personal?" rule, shared by the output cache,
/// response compression (BREACH) and page ETags. A response is personal when the request carries
/// credentials, a session cookie or a waiting flash message, or when the response sets cookies,
/// is marked private/no-store, issued a CSRF token or loaded/wrote the session.
/// </summary>
/// <remarks>
/// Safe to call before the response starts: the session cookie is written in an
/// <c>OnStarting</c> callback, so the session state itself is checked, not only <c>Set-Cookie</c>.
/// </remarks>
public static class WebResponsePrivacy
{
    /// <summary>True when the request and response together may carry personal data.</summary>
    public static bool IsPersonal(HttpContext http, WebLogicConfig? config) =>
        IsPersonalRequest(http, config) || IsPersonalResponse(http);

    /// <summary>
    /// True when the request is signed in, carries an <c>Authorization</c> header, a session
    /// cookie (when <paramref name="config"/> is known) or a waiting flash message.
    /// </summary>
    public static bool IsPersonalRequest(HttpContext http, WebLogicConfig? config)
    {
        ArgumentNullException.ThrowIfNull(http);
        if (WebSecurityContext.Get(http)?.Identity.IsAuthenticated == true || http.User.Identity?.IsAuthenticated == true)
            return true;
        if (http.Request.Headers.ContainsKey("Authorization") || WebFlash.IsPending(http))
            return true;
        return config is not null && http.Request.Cookies.ContainsKey(WebSessionCookies.SessionName(http, config.Security.Session));
    }

    /// <summary>
    /// True when the response sets cookies, is marked <c>private</c> or <c>no-store</c>, issued a
    /// CSRF token, or loaded, wrote or is about to issue a session.
    /// </summary>
    public static bool IsPersonalResponse(HttpContext http)
    {
        ArgumentNullException.ThrowIfNull(http);
        var response = http.Response;
        if (response.Headers.ContainsKey("Set-Cookie"))
            return true;
        var cacheControl = response.Headers.CacheControl.ToString();
        if (cacheControl.Contains("no-store", StringComparison.OrdinalIgnoreCase) || cacheControl.Contains("private", StringComparison.OrdinalIgnoreCase))
            return true;
        if (WebSecurityContext.Get(http) is { CsrfTokenIssued: true })
            return true;
        return http.Features.Get<IWebSession>() is LazyWebSession { Dirty: true } or LazyWebSession { IssueCookie: true } or LazyWebSession { Loaded: not null };
    }
}
