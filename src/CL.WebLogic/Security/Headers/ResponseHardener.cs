using CL.WebLogic.Hosting.Services;
using CL.WebLogic.Security.Models;
using CL.WebLogic.Security.Pipeline;
using Microsoft.AspNetCore.Http;

namespace CL.WebLogic.Security.Headers;

/// <summary>
/// Writes the security headers when the response starts — after the handler ran — so a handler
/// cannot silently weaken them. The only way to change one is
/// <see cref="WebSecurityContext.OverrideHeader"/>, which is logged.
/// </summary>
public sealed class ResponseHardener
{
    private static readonly string[] ManagedHeaders =
    [
        "Content-Security-Policy", "Content-Security-Policy-Report-Only", "Strict-Transport-Security", "X-Content-Type-Options",
        "X-Frame-Options", "Referrer-Policy", "Permissions-Policy", "Cross-Origin-Opener-Policy", "Cross-Origin-Resource-Policy"
    ];

    private readonly ICspNonceProvider _nonce;
    private readonly IWebLogicContext _context;

    /// <summary>Creates the hardener.</summary>
    public ResponseHardener(ICspNonceProvider nonce, IWebLogicContext context)
    {
        _nonce = nonce ?? throw new ArgumentNullException(nameof(nonce));
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <summary>Registers the header callback for the request.</summary>
    public void Register(WebSecurityContext security) =>
        security.HttpContext.Response.OnStarting(static state =>
        {
            var (hardener, ctx) = ((ResponseHardener, WebSecurityContext))state;
            hardener.Apply(ctx);
            return Task.CompletedTask;
        }, (this, security));

    /// <summary>Applies the headers (public for tests).</summary>
    public void Apply(WebSecurityContext security)
    {
        var httpContext = security.HttpContext;
        var headers = httpContext.Response.Headers;
        var settings = security.Settings;
        var config = settings.Config.Headers;

        foreach (var name in ManagedHeaders)
        {
            if (headers.ContainsKey(name) && !security.HeaderOverrides.ContainsKey(name))
                _context.Logger.Debug($"Handler-set '{name}' replaced by the security policy [{httpContext.TraceIdentifier}]. Use request.Security.OverrideHeader to change it.");
        }

        headers.Remove("Content-Security-Policy");
        headers.Remove("Content-Security-Policy-Report-Only");
        if (settings.ContentSecurityPolicy is { } csp)
        {
            var value = csp.Replace(CspBuilder.NoncePlaceholder, _nonce.GetNonce(httpContext), StringComparison.Ordinal);
            headers[config.CspReportOnly ? "Content-Security-Policy-Report-Only" : "Content-Security-Policy"] = value;
        }

        if (Sessions.WebSessionCookies.IsSecure(httpContext) && settings.StrictTransportSecurity is { } hsts)
            headers["Strict-Transport-Security"] = hsts;
        else
            headers.Remove("Strict-Transport-Security");

        headers["X-Content-Type-Options"] = "nosniff";
        headers.Remove("X-Powered-By");
        Set(headers, "Referrer-Policy", config.ReferrerPolicy);
        Set(headers, "Permissions-Policy", config.PermissionsPolicy);
        Set(headers, "Cross-Origin-Opener-Policy", config.CrossOriginOpenerPolicy);
        Set(headers, "Cross-Origin-Resource-Policy", config.CrossOriginResourcePolicy);
        if (config.FrameAncestors.Count == 0)
            headers["X-Frame-Options"] = "DENY";
        else
            headers.Remove("X-Frame-Options");

        if ((security.Identity.IsAuthenticated && config.NoStoreWhenAuthenticated)
            || (security.CsrfTokenIssued && !headers.ContainsKey("Cache-Control")))
        {
            headers.CacheControl = "no-store";
            headers.Pragma = "no-cache";
        }

        foreach (var (name, value) in security.HeaderOverrides)
        {
            _context.Logger.Debug($"Security header '{name}' overridden for {httpContext.Request.Path} [{httpContext.TraceIdentifier}]");
            if (string.IsNullOrEmpty(value)) headers.Remove(name);
            else headers[name] = value;
        }
    }

    private static void Set(IHeaderDictionary headers, string name, string value)
    {
        if (string.IsNullOrWhiteSpace(value)) headers.Remove(name);
        else headers[name] = value;
    }
}
