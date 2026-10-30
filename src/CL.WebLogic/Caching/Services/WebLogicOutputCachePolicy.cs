using CL.WebLogic.Security.Pipeline;
using CL.WebLogic.Security.Sessions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.OutputCaching;

namespace CL.WebLogic.Caching.Services;

/// <summary>
/// Added in front of every WebLogic output-cache policy. Lookups and storage only for anonymous
/// GET/HEAD requests without credentials, a session cookie (a page may show anonymous session
/// data such as a cart) or a waiting flash message; nothing is stored when the response sets cookies,
/// issued a CSRF token, wrote the session, is not 200 or is marked private/no-store. Entries
/// vary by host, tenant and whether the WebLogic client asked (JSON vs HTML answers).
/// </summary>
public sealed class WebLogicOutputCachePolicy : IOutputCachePolicy
{
    /// <summary>Shared instance (stateless).</summary>
    public static WebLogicOutputCachePolicy Instance { get; } = new();

    /// <inheritdoc/>
    public ValueTask CacheRequestAsync(OutputCacheContext context, CancellationToken cancellation)
    {
        ArgumentNullException.ThrowIfNull(context);
        var http = context.HttpContext;
        var security = WebSecurityContext.Get(http);
        var cacheable = HttpMethods.IsGet(http.Request.Method) || HttpMethods.IsHead(http.Request.Method);
        cacheable &= security?.Identity.IsAuthenticated != true && http.User.Identity?.IsAuthenticated != true;
        cacheable &= !http.Request.Headers.ContainsKey("Authorization");
        cacheable &= !Runtime.Services.WebFlash.IsPending(http);
        if (http.RequestServices?.GetService(typeof(Configuration.Services.WebLogicConfigHolder)) is Configuration.Services.WebLogicConfigHolder config)
            cacheable &= !http.Request.Cookies.ContainsKey(WebSessionCookies.SessionName(http, config.Current.Security.Session));

        context.EnableOutputCaching = true;
        context.AllowCacheLookup = cacheable;
        context.AllowCacheStorage = cacheable;
        context.AllowLocking = true;
        context.CacheVaryByRules.VaryByHost = true;
        context.CacheVaryByRules.HeaderNames = new Microsoft.Extensions.Primitives.StringValues(["X-Requested-With", "Accept"]);
        context.CacheVaryByRules.VaryByValues["wl-tenant"] = security?.TenantId ?? "";
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask ServeFromCacheAsync(OutputCacheContext context, CancellationToken cancellation) => ValueTask.CompletedTask;

    /// <inheritdoc/>
    public ValueTask ServeResponseAsync(OutputCacheContext context, CancellationToken cancellation)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!IsStorable(context.HttpContext))
            context.AllowCacheStorage = false;
        return ValueTask.CompletedTask;
    }

    /// <summary>True when the response may be shared with other visitors.</summary>
    public static bool IsStorable(HttpContext http)
    {
        ArgumentNullException.ThrowIfNull(http);
        var response = http.Response;
        if (response.StatusCode != StatusCodes.Status200OK || response.Headers.ContainsKey("Set-Cookie"))
            return false;
        var cacheControl = response.Headers.CacheControl.ToString();
        if (cacheControl.Contains("no-store", StringComparison.OrdinalIgnoreCase) || cacheControl.Contains("private", StringComparison.OrdinalIgnoreCase))
            return false;
        if (WebSecurityContext.Get(http) is { CsrfTokenIssued: true })
            return false;
        return http.Features.Get<IWebSession>() is not LazyWebSession { Dirty: true } and not LazyWebSession { IssueCookie: true } and not LazyWebSession { Loaded: not null };
    }
}
