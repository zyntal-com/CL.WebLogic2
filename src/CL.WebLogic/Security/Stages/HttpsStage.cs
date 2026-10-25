using CL.WebLogic.Results;
using CL.WebLogic.Security.Pipeline;
using Microsoft.AspNetCore.Http;

namespace CL.WebLogic.Security.Stages;

/// <summary>
/// Redirects HTTP to HTTPS (308, method preserved). The target host comes from
/// <c>PublicBaseUrl</c> — or an allowed Host header — never from an unvalidated Host header.
/// </summary>
public sealed class HttpsStage : IWebSecurityStage
{
    /// <inheritdoc/>
    public string Name => "https";

    /// <inheritdoc/>
    public WebSecurityPhase Phase => WebSecurityPhase.PreRouting;

    /// <inheritdoc/>
    public int Order => 200;

    /// <inheritdoc/>
    public ValueTask<WebSecurityDecision> ExecuteAsync(WebSecurityContext context, CancellationToken cancellationToken)
    {
        var request = context.HttpContext.Request;
        var settings = context.Settings;
        if (!settings.Config.Https.Enforce || request.IsHttps || settings.IsProbePath(request.Path.Value))
            return ValueTask.FromResult(WebSecurityDecision.Continue);

        string authority;
        if (settings.PublicBaseUri is { } baseUri)
            authority = baseUri.Scheme == Uri.UriSchemeHttps ? baseUri.Authority : baseUri.Host;
        else if (!settings.AllowedHosts.AllowsAny && settings.AllowedHosts.IsAllowed(request.Host.Host))
            authority = request.Host.Host;
        else
            return ValueTask.FromResult(WebSecurityDecision.Reject(StatusCodes.Status400BadRequest, "https-required-no-canonical-host"));

        var target = new Uri("https://" + authority + request.PathBase + request.Path + request.QueryString);
        return ValueTask.FromResult(WebSecurityDecision.ShortCircuit(WebResult.ExternalRedirect(target, permanent: true, preserveMethod: true), "https-redirect"));
    }
}
