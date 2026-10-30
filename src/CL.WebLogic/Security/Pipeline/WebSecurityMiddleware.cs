using CL.WebLogic.Diagnostics.Services;
using CL.WebLogic.Security.Headers;
using CL.WebLogic.Security.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace CL.WebLogic.Security.Pipeline;

/// <summary>
/// Runs the <see cref="WebSecurityPhase.PreRouting"/> stages and registers response hardening.
/// Sits right after the exception boundary, before routing.
/// </summary>
public sealed class WebSecurityMiddleware
{
    private readonly RequestDelegate _next;
    private readonly WebSecurityPipeline _pipeline;
    private readonly WebSecuritySettingsProvider _settings;
    private readonly WebSecurityResponder _responder;
    private readonly ResponseHardener _hardener;
    private readonly WebLogicMetrics? _metrics;

    /// <summary>Creates the middleware.</summary>
    public WebSecurityMiddleware(RequestDelegate next, WebSecurityPipeline pipeline, WebSecuritySettingsProvider settings, WebSecurityResponder responder, ResponseHardener hardener)
        : this(next, pipeline, settings, responder, hardener, null)
    {
    }

    /// <summary>Creates the middleware with tracing (a span per run when a listener samples it).</summary>
    [ActivatorUtilitiesConstructor]
    public WebSecurityMiddleware(RequestDelegate next, WebSecurityPipeline pipeline, WebSecuritySettingsProvider settings, WebSecurityResponder responder, ResponseHardener hardener, WebLogicMetrics? metrics)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _responder = responder ?? throw new ArgumentNullException(nameof(responder));
        _hardener = hardener ?? throw new ArgumentNullException(nameof(hardener));
        _metrics = metrics;
    }

    /// <summary>Processes the request.</summary>
    public async Task InvokeAsync(HttpContext httpContext)
    {
        var security = new WebSecurityContext(httpContext, _settings.Current);
        httpContext.Features.Set(security);
        _hardener.Register(security);

        var cancellationToken = httpContext.RequestAborted;
        using (var activity = _metrics?.StartSecurityPhase(WebSecurityPhase.PreRouting))
        {
            foreach (var stage in _pipeline.GetStages(WebSecurityPhase.PreRouting))
            {
                var decision = await stage.ExecuteAsync(security, cancellationToken).ConfigureAwait(false);
                if (!decision.IsContinue)
                {
                    WebLogicMetrics.MarkRejected(activity, stage.Name, decision.Reason, decision.StatusCode);
                    await _responder.WriteAsync(security, stage, decision, cancellationToken).ConfigureAwait(false);
                    return;
                }
            }
            activity?.SetTag(Diagnostics.Models.WebLogicInstruments.SecurityOutcomeTag, "passed");
        }

        await _next(httpContext).ConfigureAwait(false);
    }
}
