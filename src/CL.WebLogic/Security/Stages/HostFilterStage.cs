using CL.WebLogic.Results;
using CL.WebLogic.Security.Pipeline;
using Microsoft.AspNetCore.Http;

namespace CL.WebLogic.Security.Stages;

/// <summary>Refuses unknown Host headers (400) and optionally redirects to the canonical host from <c>PublicBaseUrl</c>.</summary>
public sealed class HostFilterStage : IWebSecurityStage
{
    /// <inheritdoc/>
    public string Name => "host-filter";

    /// <inheritdoc/>
    public WebSecurityPhase Phase => WebSecurityPhase.PreRouting;

    /// <inheritdoc/>
    public int Order => 150;

    /// <inheritdoc/>
    public ValueTask<WebSecurityDecision> ExecuteAsync(WebSecurityContext context, CancellationToken cancellationToken)
    {
        var request = context.HttpContext.Request;
        var settings = context.Settings;
        if (settings.IsProbePath(request.Path.Value))
            return ValueTask.FromResult(WebSecurityDecision.Continue);

        var host = request.Host.Host;
        if (!settings.AllowedHosts.IsAllowed(host))
            return ValueTask.FromResult(WebSecurityDecision.Reject(StatusCodes.Status400BadRequest, "host-not-allowed"));

        var canonical = settings.PublicBaseUri;
        if (settings.Config.Hosts.RedirectToCanonical && canonical is not null
            && !string.Equals(host, canonical.Host, StringComparison.OrdinalIgnoreCase))
        {
            var target = new Uri(canonical.GetLeftPart(UriPartial.Authority) + request.PathBase + request.Path + request.QueryString);
            var preserve = !HttpMethods.IsGet(request.Method) && !HttpMethods.IsHead(request.Method);
            return ValueTask.FromResult(WebSecurityDecision.ShortCircuit(WebResult.ExternalRedirect(target, permanent: true, preserveMethod: preserve), "canonical-host"));
        }

        return ValueTask.FromResult(WebSecurityDecision.Continue);
    }
}
