using CL.WebLogic.Security.Identity;
using Microsoft.AspNetCore.Http;

namespace CL.WebLogic.Templates.Rendering;

/// <summary>
/// Claims-based authorizer (used outside the security pipeline, e.g. in unit tests): groups are
/// role claims; permissions are <c>permission</c> claims, matched with <see cref="WebPermissionMatcher"/>.
/// </summary>
public sealed class ClaimsTemplateAuthorizer : ITemplateAuthorizer
{
    /// <summary>Claim type holding permissions.</summary>
    public const string PermissionClaimType = "permission";

    /// <inheritdoc/>
    public bool IsInAnyGroup(HttpContext? httpContext, IReadOnlyList<string> groups)
    {
        var user = httpContext?.User;
        return user?.Identity?.IsAuthenticated == true && groups.Any(user.IsInRole);
    }

    /// <inheritdoc/>
    public bool HasPermission(HttpContext? httpContext, string permission)
    {
        var user = httpContext?.User;
        return user?.Identity?.IsAuthenticated == true
            && WebPermissionMatcher.Grants(user.FindAll(PermissionClaimType).Select(c => c.Value), permission);
    }
}
