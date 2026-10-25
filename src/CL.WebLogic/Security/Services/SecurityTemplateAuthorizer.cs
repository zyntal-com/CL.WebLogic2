using CL.WebLogic.Security.Authorization;
using CL.WebLogic.Security.Identity;
using CL.WebLogic.Security.Pipeline;
using CL.WebLogic.Templates.Rendering;
using Microsoft.AspNetCore.Http;

namespace CL.WebLogic.Security.Services;

/// <summary>
/// Template group / permission checks against the request's <see cref="WebIdentity"/>, using the
/// permissions resolved by <see cref="PermissionTemplatePreparer"/> before rendering.
/// </summary>
public sealed class SecurityTemplateAuthorizer : ITemplateAuthorizer
{
    private static readonly ClaimsTemplateAuthorizer Fallback = new();

    /// <inheritdoc/>
    public bool IsInAnyGroup(HttpContext? httpContext, IReadOnlyList<string> groups)
    {
        if (httpContext is null || WebSecurityContext.Get(httpContext) is not { } security)
            return Fallback.IsInAnyGroup(httpContext, groups);
        var identity = security.Identity;
        return identity.IsAuthenticated && groups.Any(g => identity.Groups.Contains(g, StringComparer.OrdinalIgnoreCase));
    }

    /// <inheritdoc/>
    public bool HasPermission(HttpContext? httpContext, string permission)
    {
        if (httpContext is null || WebSecurityContext.Get(httpContext) is not { } security)
            return Fallback.HasPermission(httpContext, permission);
        var identity = security.Identity;
        return identity.IsAuthenticated
            && WebPermissionMatcher.Grants(WebPermissionService.Peek(security) ?? identity.Permissions, permission);
    }
}
