using CL.WebLogic.Security.Authorization;
using CL.WebLogic.Security.Pipeline;
using CL.WebLogic.Templates.Rendering;
using Microsoft.AspNetCore.Http;

namespace CL.WebLogic.Security.Services;

/// <summary>Resolves the signed-in user's permissions before a page renders (once per request).</summary>
public sealed class PermissionTemplatePreparer : ITemplateRenderPreparer
{
    private readonly WebPermissionService _permissions;

    /// <summary>Creates the preparer.</summary>
    public PermissionTemplatePreparer(WebPermissionService permissions) =>
        _permissions = permissions ?? throw new ArgumentNullException(nameof(permissions));

    /// <inheritdoc/>
    public async ValueTask PrepareAsync(HttpContext httpContext, CancellationToken cancellationToken)
    {
        if (WebSecurityContext.Get(httpContext) is { Identity.IsAuthenticated: true } security)
            await _permissions.GetAsync(security, cancellationToken).ConfigureAwait(false);
    }
}
