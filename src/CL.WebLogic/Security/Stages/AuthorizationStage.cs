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
/// requests are redirected to the login path (per route or area) with a safe return URL; API
/// requests get 401. Step-up failures (<c>RequireRecentSignIn</c>) ask the user to sign in again.
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

        // Groups and permissions come before step-up: nobody is asked to sign in again for a page
        // they could not open anyway.
        var requirements = new List<IWebAuthorizationRequirement> { WebAuthorizationRequirements.Authenticated };
        if (route.RequiredGroups.Count > 0)
            requirements.Add(WebAuthorizationRequirements.AnyGroup([.. route.RequiredGroups]));
        if (route.RequiredPermissions.Count > 0)
            requirements.Add(WebAuthorizationRequirements.Permissions([.. route.RequiredPermissions]));
        if (route.RecentSignIn is { } recent)
            requirements.Add(recent);

        var result = await _authorization.AuthorizeAsync(context, requirements, null, cancellationToken).ConfigureAwait(false);
        foreach (var policy in route.Policies)
        {
            if (!result.Succeeded)
                break;
            result = await _authorization.AuthorizeAsync(context, policy, null, cancellationToken).ConfigureAwait(false);
        }

        if (result.Succeeded)
            return WebSecurityDecision.Continue;
        if (result.Reauthenticate)
            return Challenge(context, route, reauthenticate: true);
        if (result.Challenge)
            return Challenge(context, route, reauthenticate: false);
        if (route.AccessDeniedPath is { } denied && IsPageGet(context, route))
            return WebSecurityDecision.ShortCircuit(WebResult.Redirect(WithReturnUrl(denied, context)), "forbidden:" + result.FailedRequirement);
        return WebSecurityDecision.Reject(StatusCodes.Status403Forbidden, "forbidden:" + result.FailedRequirement);
    }

    private static WebSecurityDecision Challenge(WebSecurityContext context, WebRouteMetadata route, bool reauthenticate)
    {
        var reason = reauthenticate ? "reauthentication-required" : "login-required";
        if (IsPageGet(context, route))
        {
            var login = WithReturnUrl(route.LoginPath ?? context.Settings.Config.LoginPath, context, reauthenticate);
            return WebSecurityDecision.ShortCircuit(WebResult.Redirect(login), reason);
        }

        var detail = reauthenticate ? "Sign in again to continue." : "Authentication is required.";
        WebResult challenge = DefaultErrorPageRenderer.WantsProblemJson(context.HttpContext)
            ? WebResult.Problem(StatusCodes.Status401Unauthorized, detail: detail)
            : WebResult.Status(StatusCodes.Status401Unauthorized);
        challenge.WithHeader("WWW-Authenticate", reauthenticate ? "WebLogic error=\"reauthentication_required\"" : "WebLogic");
        return WebSecurityDecision.ShortCircuit(challenge, reauthenticate ? reason : "authentication-required");
    }

    private static bool IsPageGet(WebSecurityContext context, WebRouteMetadata route) =>
        route.Kind is WebRouteKind.Page or WebRouteKind.Fallback
        && HttpMethods.IsGet(context.HttpContext.Request.Method)
        && !DefaultErrorPageRenderer.WantsProblemJson(context.HttpContext);

    /// <summary><paramref name="path"/> with the current page as a safe <c>returnUrl</c> (and <c>reauth=1</c>).</summary>
    private static string WithReturnUrl(string path, WebSecurityContext context, bool reauthenticate = false)
    {
        var request = context.HttpContext.Request;
        var returnUrl = WebUrl.SafeReturnUrl(request.Path + request.QueryString);
        return path + (path.Contains('?', StringComparison.Ordinal) ? "&" : "?")
            + (reauthenticate ? "reauth=1&" : "") + "returnUrl=" + Uri.EscapeDataString(returnUrl);
    }
}
