using CL.WebLogic.Hosting.Services;
using CL.WebLogic.Results;
using CL.WebLogic.Routing.Models;
using CL.WebLogic.Runtime.Models;
using CL.WebLogic.Runtime.Services;
using CL.WebLogic.Security.Audit;
using CL.WebLogic.Security.Events;
using Microsoft.AspNetCore.Http;

namespace CL.WebLogic.Security.Pipeline;

/// <summary>Writes stage rejections, audits them and publishes <see cref="WebSecurityRejectedEvent"/>.</summary>
public sealed class WebSecurityResponder
{
    private static readonly WebRouteMetadata SystemRoute = new()
    {
        Kind = WebRouteKind.System,
        Pattern = "/",
        Methods = [],
        Contributor = new WebContributorDescriptor { Id = "weblogic.security", Name = "WebLogic security" },
        AllowAnonymous = true
    };

    private readonly IWebAuditLog _audit;
    private readonly IWebLogicContext _context;

    /// <summary>Creates the responder.</summary>
    public WebSecurityResponder(IWebAuditLog audit, IWebLogicContext context)
    {
        _audit = audit ?? throw new ArgumentNullException(nameof(audit));
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <summary>Writes the response for a short-circuit <paramref name="decision"/> of <paramref name="stage"/>.</summary>
    public async Task WriteAsync(WebSecurityContext security, IWebSecurityStage stage, WebSecurityDecision decision, CancellationToken cancellationToken)
    {
        var httpContext = security.HttpContext;
        var reason = decision.Reason ?? "rejected";
        _context.Logger.Debug($"Security stage '{stage.Name}' stopped {httpContext.Request.Method} {httpContext.Request.Path} with {decision.StatusCode}: {reason} [{httpContext.TraceIdentifier}]");

        _audit.Record(security, "security." + stage.Name, reason, decision.StatusCode);
        await _context.Events.PublishAsync(new WebSecurityRejectedEvent(stage.Name, reason, httpContext.Request.Path.Value ?? "/", decision.StatusCode, decision.RuleId)).ConfigureAwait(false);

        var result = decision.Result ?? (DefaultErrorPageRenderer.WantsProblemJson(httpContext)
            ? WebResult.Problem(decision.StatusCode)
            : WebResult.Status(decision.StatusCode));

        var route = httpContext.GetEndpoint()?.Metadata.GetMetadata<WebRouteMetadata>() ?? SystemRoute;
        await result.ExecuteAsync(new WebRequestContext(httpContext, route, WebJson.Default), cancellationToken).ConfigureAwait(false);
    }
}
