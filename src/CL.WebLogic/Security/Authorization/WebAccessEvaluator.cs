using CL.WebLogic.Security.Pipeline;

namespace CL.WebLogic.Security.Authorization;

/// <summary>Evaluates <see cref="WebAccessRule"/>s with the authorization service.</summary>
public sealed class WebAccessEvaluator
{
    private readonly IWebAuthorizationService _authorization;

    /// <summary>Creates the evaluator.</summary>
    public WebAccessEvaluator(IWebAuthorizationService authorization) =>
        _authorization = authorization ?? throw new ArgumentNullException(nameof(authorization));

    /// <summary>True when the caller of <paramref name="security"/> passes <paramref name="rule"/>; no security context means anonymous.</summary>
    public async ValueTask<bool> AllowsAsync(WebSecurityContext? security, WebAccessRule rule, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(rule);
        if (rule.AllowAnonymous)
            return true;
        if (security is null || !security.Identity.IsAuthenticated)
            return false;

        var requirements = new List<IWebAuthorizationRequirement> { WebAuthorizationRequirements.Authenticated };
        if (rule.Groups.Count > 0)
            requirements.Add(WebAuthorizationRequirements.AnyGroup([.. rule.Groups]));
        if (rule.Permissions.Count > 0)
            requirements.Add(WebAuthorizationRequirements.Permissions([.. rule.Permissions]));
        var result = await _authorization.AuthorizeAsync(security, requirements, null, cancellationToken).ConfigureAwait(false);
        if (!result.Succeeded)
            return false;
        return rule.Policy is null || (await _authorization.AuthorizeAsync(security, rule.Policy, null, cancellationToken).ConfigureAwait(false)).Succeeded;
    }
}
