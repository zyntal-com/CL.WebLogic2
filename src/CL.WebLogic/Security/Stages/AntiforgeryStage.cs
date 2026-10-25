using CL.WebLogic.Security.Antiforgery;
using CL.WebLogic.Security.Models;
using CL.WebLogic.Security.Pipeline;
using CL.WebLogic.Security.Sessions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Antiforgery;

namespace CL.WebLogic.Security.Stages;

/// <summary>
/// CSRF protection for every unsafe request — anonymous ones and the fallback route included.
/// Checks <c>Sec-Fetch-Site</c> and <c>Origin</c>/<c>Referer</c>, then the synchronizer token
/// (header or form field). Exempt: routes with <c>AllowCrossSitePost()</c>, endpoints with
/// <c>DisableAntiforgery()</c>, non-ambient callers (API key / bearer) that sent no browser cookies,
/// and non-WebLogic endpoints called without WebLogic cookies.
/// </summary>
public sealed class AntiforgeryStage : IWebSecurityStage
{
    private readonly IWebCsrfTokens _tokens;

    /// <summary>Creates the stage.</summary>
    public AntiforgeryStage(IWebCsrfTokens tokens) => _tokens = tokens ?? throw new ArgumentNullException(nameof(tokens));

    /// <inheritdoc/>
    public string Name => "antiforgery";

    /// <inheritdoc/>
    public WebSecurityPhase Phase => WebSecurityPhase.Endpoint;

    /// <inheritdoc/>
    public int Order => 800;

    /// <inheritdoc/>
    public async ValueTask<WebSecurityDecision> ExecuteAsync(WebSecurityContext context, CancellationToken cancellationToken)
    {
        var httpContext = context.HttpContext;
        var request = httpContext.Request;
        if (HttpMethods.IsGet(request.Method) || HttpMethods.IsHead(request.Method) || HttpMethods.IsOptions(request.Method) || HttpMethods.IsTrace(request.Method))
            return WebSecurityDecision.Continue;
        if (context.Route?.AllowCrossSitePost == true
            || httpContext.GetEndpoint()?.Metadata.GetMetadata<IAntiforgeryMetadata>() is { RequiresValidation: false })
            return WebSecurityDecision.Continue;

        var config = context.Settings.Config;
        var hasBrowserCookies = request.Cookies.ContainsKey(WebSessionCookies.SessionName(httpContext, config.Session))
            || request.Cookies.ContainsKey(WebSessionCookies.Name(httpContext, config.Csrf.PreSessionCookieName));
        if (context.Identity.IsAuthenticated && !context.Identity.IsAmbient && !hasBrowserCookies)
            return WebSecurityDecision.Continue;

        // WebLogic routes are always checked (login CSRF included). Other endpoints (minimal APIs,
        // ASP.NET's 405 endpoint…) are checked when the browser sent WebLogic cookies.
        if (context.Route is null && !hasBrowserCookies && !context.Identity.IsAmbient)
            return WebSecurityDecision.Continue;

        if (string.Equals(request.Headers["Sec-Fetch-Site"].ToString(), "cross-site", StringComparison.OrdinalIgnoreCase)
            && !IsTrusted(context, request.Headers.Origin.ToString()))
            return WebSecurityDecision.Reject(StatusCodes.Status403Forbidden, "csrf-cross-site-fetch");

        var origin = request.Headers.Origin.ToString();
        if (string.IsNullOrEmpty(origin) || origin == "null")
            origin = request.Headers.Referer.ToString();
        if (!string.IsNullOrEmpty(origin) && !IsSameOrTrusted(context, origin))
            return WebSecurityDecision.Reject(StatusCodes.Status403Forbidden, "csrf-origin-mismatch");

        var token = request.Headers[config.Csrf.HeaderName].ToString();
        if (string.IsNullOrEmpty(token) && request.HasFormContentType)
        {
            var form = await request.ReadFormAsync(cancellationToken).ConfigureAwait(false);
            token = form[config.Csrf.FieldName].ToString();
        }

        return _tokens.Validate(httpContext, token)
            ? WebSecurityDecision.Continue
            : WebSecurityDecision.Reject(StatusCodes.Status403Forbidden, string.IsNullOrEmpty(token) ? "csrf-token-missing" : "csrf-token-invalid");
    }

    private static bool IsSameOrTrusted(WebSecurityContext context, string originOrReferer)
    {
        var origin = WebSecuritySettings.NormalizeOrigin(originOrReferer);
        if (origin.Length == 0)
            return false;
        var request = context.HttpContext.Request;
        var own = request.Scheme + "://" + request.Host.Value;
        return string.Equals(origin, own, StringComparison.OrdinalIgnoreCase)
            || (context.Settings.PublicBaseUri is { } baseUri && string.Equals(origin, baseUri.GetLeftPart(UriPartial.Authority), StringComparison.OrdinalIgnoreCase))
            || context.Settings.TrustedOrigins.Contains(origin);
    }

    private static bool IsTrusted(WebSecurityContext context, string origin) =>
        context.Settings.TrustedOrigins.Contains(WebSecuritySettings.NormalizeOrigin(origin));
}
