using CL.WebLogic.Hosting.Services;
using CL.WebLogic.Results;
using CL.WebLogic.Routing.Models;
using CL.WebLogic.Runtime.Services;
using CL.WebLogic.Security.Authorization;
using CL.WebLogic.Security.Pipeline;
using Microsoft.AspNetCore.Http;

namespace CL.WebLogic.Security.Stages;

/// <summary>
/// Authorizes WebLogic routes: every route requires an authenticated user unless it opted into
/// <c>AllowAnonymous()</c>, plus its groups, permissions and named policies. Unauthenticated page
/// requests are redirected to the login path with a safe return URL; API requests get 401.
/// </summary>
public sealed class AuthorizationStage : IWebSecurityStage
{
    private readonly IWebAuthorizationService _authorization;

    /// <summary>Creates the stage.</summary>
    public AuthorizationStage(IWebAuthorizationService authorization) =>
        _authorization = authorization ?? throw new ArgumentNullException(nameof(authorization));

    /// <inheritdoc/>
    public string Name => "authorization";

    /// <inheritdoc/>
    public WebSecurityPhase Phase => WebSecurityPhase.Endpoint;

    /// <inheritdoc/>
    public int Order => 700;

    /// <inheritdoc/>
    public async ValueTask<WebSecurityDecision> ExecuteAsync(WebSecurityContext context, CancellationToken cancellationToken)
    {
        var route = context.Route;
        if (route is null || route.AllowAnonymous)
            return WebSecurityDecision.Continue;

        var requirements = new List<IWebAuthorizationRequirement> { WebAuthorizationRequirements.Authenticated };
        if (route.RequiredGroups.Count > 0)
            requirements.Add(WebAuthorizationRequirements.AnyGroup([.. route.RequiredGroups]));
        if (route.RequiredPermissions.Count > 0)
            requirements.Add(WebAuthorizationRequirements.Permissions([.. route.RequiredPermissions]));

        var result = await _authorization.AuthorizeAsync(context, requirements, null, cancellationToken).ConfigureAwait(false);
        foreach (var policy in route.Policies)
        {
            if (!result.Succeeded)
                break;
            result = await _authorization.AuthorizeAsync(context, policy, null, cancellationToken).ConfigureAwait(false);
        }

        if (result.Succeeded)
            return WebSecurityDecision.Continue;
        if (!result.Challenge)
            return WebSecurityDecision.Reject(StatusCodes.Status403Forbidden, "forbidden:" + result.FailedRequirement);
        return Challenge(context, route);
    }

    private static WebSecurityDecision Challenge(WebSecurityContext context, WebRouteMetadata route)
    {
        var httpContext = context.HttpContext;
        var request = httpContext.Request;
        var isPage = route.Kind is WebRouteKind.Page or WebRouteKind.Fallback;
        if (isPage && HttpMethods.IsGet(request.Method) && !DefaultErrorPageRenderer.WantsProblemJson(httpContext))
        {
            var returnUrl = WebUrl.SafeReturnUrl(request.Path + request.QueryString);
            var login = context.Settings.Config.LoginPath + "?returnUrl=" + Uri.EscapeDataString(returnUrl);
            return WebSecurityDecision.ShortCircuit(WebResult.Redirect(login), "login-required");
        }

        WebResult challenge = DefaultErrorPageRenderer.WantsProblemJson(httpContext)
            ? WebResult.Problem(StatusCodes.Status401Unauthorized, detail: "Authentication is required.")
            : WebResult.Status(StatusCodes.Status401Unauthorized);
        challenge.WithHeader("WWW-Authenticate", "WebLogic");
        return WebSecurityDecision.ShortCircuit(challenge, "authentication-required");
    }
}
