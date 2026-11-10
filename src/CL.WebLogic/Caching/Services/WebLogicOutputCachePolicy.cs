using CL.WebLogic.Security.Headers;
using CL.WebLogic.Security.Pipeline;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.OutputCaching;

namespace CL.WebLogic.Caching.Services;

/// <summary>
/// Added in front of every WebLogic output-cache policy. Lookups and storage only for anonymous
/// GET/HEAD requests without credentials, a session cookie (a page may show anonymous session
/// data such as a cart) or a waiting flash message; nothing is stored when the response sets cookies,
/// issued a CSRF token, wrote the session, is not 200 or is marked private/no-store. Entries
/// vary by host, tenant, request culture (when localization is enabled) and whether the WebLogic
/// client asked (JSON vs HTML answers).
/// </summary>
public sealed class WebLogicOutputCachePolicy : IOutputCachePolicy
{
    /// <summary>Shared instance (stateless).</summary>
    public static WebLogicOutputCachePolicy Instance { get; } = new();

    private static readonly object ServedFromCacheKey = new();

    /// <inheritdoc/>
    public ValueTask CacheRequestAsync(OutputCacheContext context, CancellationToken cancellation)
    {
        ArgumentNullException.ThrowIfNull(context);
        var http = context.HttpContext;
        var security = WebSecurityContext.Get(http);
        var cacheable = HttpMethods.IsGet(http.Request.Method) || HttpMethods.IsHead(http.Request.Method);
        var config = http.RequestServices?.GetService(typeof(Configuration.Services.WebLogicConfigHolder)) as Configuration.Services.WebLogicConfigHolder;
        cacheable &= !WebResponsePrivacy.IsPersonalRequest(http, config?.Current);

        // The stored HTML holds the nonce of the request that rendered it. The output cache keeps the headers as they
        // are when the response starts, which is before the Content-Security-Policy header is written, so the nonce
        // travels in a header of its own: a hit gets it back and answers with the same nonce (see ResponseHardener,
        // which also removes the header again).
        if (cacheable && http.RequestServices?.GetService(typeof(ICspNonceProvider)) is ICspNonceProvider nonces)
            http.Response.Headers[CspNonceProvider.CachedNonceHeader] = nonces.GetNonce(http);

        context.EnableOutputCaching = true;
        context.AllowCacheLookup = cacheable;
        context.AllowCacheStorage = cacheable;
        context.AllowLocking = true;
        context.CacheVaryByRules.VaryByHost = true;
        context.CacheVaryByRules.HeaderNames = new Microsoft.Extensions.Primitives.StringValues(["X-Requested-With", "Accept"]);
        context.CacheVaryByRules.VaryByValues["wl-tenant"] = security?.TenantId ?? "";
        if (Localization.Models.WebRequestCulture.Get(http) is { } culture)
            context.CacheVaryByRules.VaryByValues["wl-culture"] = culture.Name;
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask ServeFromCacheAsync(OutputCacheContext context, CancellationToken cancellation)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.HttpContext.Items[ServedFromCacheKey] = true;
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask ServeResponseAsync(OutputCacheContext context, CancellationToken cancellation)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!IsStorable(context.HttpContext))
            context.AllowCacheStorage = false;
        return ValueTask.CompletedTask;
    }

    /// <summary>True when this response comes from the output cache.</summary>
    internal static bool IsServedFromCache(HttpContext http) => http.Items.ContainsKey(ServedFromCacheKey);

    /// <summary>True when the response may be shared with other visitors.</summary>
    public static bool IsStorable(HttpContext http)
    {
        ArgumentNullException.ThrowIfNull(http);
        return http.Response.StatusCode == StatusCodes.Status200OK && !WebResponsePrivacy.IsPersonalResponse(http);
    }
}
