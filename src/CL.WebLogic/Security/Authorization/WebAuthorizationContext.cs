using CL.WebLogic.Routing.Models;
using CL.WebLogic.Security.Identity;
using CL.WebLogic.Security.Pipeline;
using Microsoft.AspNetCore.Http;

namespace CL.WebLogic.Security.Authorization;

/// <summary>What a requirement is evaluated against.</summary>
public sealed class WebAuthorizationContext
{
    private readonly WebPermissionService _permissions;

    internal WebAuthorizationContext(WebSecurityContext security, object? resource, WebPermissionService permissions)
    {
        Security = security;
        Resource = resource;
        _permissions = permissions;
    }

    /// <summary>The request's security state.</summary>
    public WebSecurityContext Security { get; }

    /// <summary>The identity being authorized.</summary>
    public WebIdentity Identity => Security.Identity;

    /// <summary>The request.</summary>
    public HttpContext HttpContext => Security.HttpContext;

    /// <summary>The WebLogic route, when authorizing a route.</summary>
    public WebRouteMetadata? Route => Security.Route;

    /// <summary>The resource for resource-based checks (e.g. the post being edited), or null.</summary>
    public object? Resource { get; }

    /// <summary>The identity's effective permissions (resolved once per request).</summary>
    public ValueTask<IReadOnlyCollection<string>> GetPermissionsAsync(CancellationToken cancellationToken) =>
        _permissions.GetAsync(Security, cancellationToken);
}
