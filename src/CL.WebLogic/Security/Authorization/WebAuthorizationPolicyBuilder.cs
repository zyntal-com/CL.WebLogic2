namespace CL.WebLogic.Security.Authorization;

/// <summary>Fluent builder for <see cref="WebAuthorizationPolicy"/>.</summary>
public sealed class WebAuthorizationPolicyBuilder
{
    private readonly List<IWebAuthorizationRequirement> _requirements = [WebAuthorizationRequirements.Authenticated];

    /// <summary>Requires membership of any of the groups.</summary>
    public WebAuthorizationPolicyBuilder RequireAnyGroup(params string[] groups) => Add(WebAuthorizationRequirements.AnyGroup(groups));

    /// <summary>Requires membership of all the groups.</summary>
    public WebAuthorizationPolicyBuilder RequireAllGroups(params string[] groups) => Add(WebAuthorizationRequirements.AllGroups(groups));

    /// <summary>Requires all the permissions.</summary>
    public WebAuthorizationPolicyBuilder RequirePermissions(params string[] permissions) => Add(WebAuthorizationRequirements.Permissions(permissions));

    /// <summary>Requires one of the authentication schemes.</summary>
    public WebAuthorizationPolicyBuilder RequireScheme(params string[] schemes) => Add(WebAuthorizationRequirements.Scheme(schemes));

    /// <summary>Requires all the scopes.</summary>
    public WebAuthorizationPolicyBuilder RequireScopes(params string[] scopes) => Add(WebAuthorizationRequirements.Scopes(scopes));

    /// <summary>Requires the identity to belong to the request's tenant.</summary>
    public WebAuthorizationPolicyBuilder RequireSameTenant() => Add(WebAuthorizationRequirements.SameTenant);

    /// <summary>Requires credentials proven within <paramref name="maxAge"/> with all of <paramref name="methods"/> (step-up).</summary>
    public WebAuthorizationPolicyBuilder RequireRecentSignIn(TimeSpan maxAge, params string[] methods) => Add(WebAuthorizationRequirements.RecentSignIn(maxAge, methods));

    /// <summary>Adds a custom requirement.</summary>
    public WebAuthorizationPolicyBuilder Require(IWebAuthorizationRequirement requirement) => Add(requirement ?? throw new ArgumentNullException(nameof(requirement)));

    /// <summary>Adds a custom check.</summary>
    public WebAuthorizationPolicyBuilder RequireAssertion(string description, Func<WebAuthorizationContext, CancellationToken, ValueTask<bool>> check) =>
        Add(WebAuthorizationRequirements.Assert(description, check));

    /// <summary>Builds the policy.</summary>
    public WebAuthorizationPolicy Build(string name) => new(name, [.. _requirements]);

    private WebAuthorizationPolicyBuilder Add(IWebAuthorizationRequirement requirement)
    {
        _requirements.Add(requirement);
        return this;
    }
}
