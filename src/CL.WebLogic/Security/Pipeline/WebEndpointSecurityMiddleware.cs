using CL.WebLogic.Routing.Models;
using Microsoft.AspNetCore.Http;

namespace CL.WebLogic.Security.Pipeline;

/// <summary>
/// Runs the <see cref="WebSecurityPhase.Endpoint"/> stages once routing has selected an endpoint.
/// Placed before output caching, so a cached response can never skip authorization.
/// </summary>
public sealed class WebEndpointSecurityMiddleware
{
    private readonly RequestDelegate _next;
    private readonly WebSecurityPipeline _pipeline;
    private readonly WebSecurityResponder _responder;

    /// <summary>Creates the middleware.</summary>
    public WebEndpointSecurityMiddleware(RequestDelegate next, WebSecurityPipeline pipeline, WebSecurityResponder responder)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));
        _responder = responder ?? throw new ArgumentNullException(nameof(responder));
    }

    /// <summary>Processes the request.</summary>
    public async Task InvokeAsync(HttpContext httpContext)
    {
        var security = WebSecurityContext.Get(httpContext);
        var endpoint = httpContext.GetEndpoint();
        if (security is null || endpoint is null)
        {
            await _next(httpContext).ConfigureAwait(false);
            return;
        }

        security.Route = endpoint.Metadata.GetMetadata<WebRouteMetadata>();
        var cancellationToken = httpContext.RequestAborted;
        foreach (var stage in _pipeline.GetStages(WebSecurityPhase.Endpoint))
        {
            var decision = await stage.ExecuteAsync(security, cancellationToken).ConfigureAwait(false);
            if (!decision.IsContinue)
            {
                await _responder.WriteAsync(security, stage, decision, cancellationToken).ConfigureAwait(false);
                return;
            }
        }

        await _next(httpContext).ConfigureAwait(false);
    }
}
