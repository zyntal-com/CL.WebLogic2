using CL.WebLogic.Security.Identity;

namespace CL.WebLogic.Security.Authorization;

/// <summary>The built-in requirements.</summary>
public static class WebAuthorizationRequirements
{
    /// <summary>The request is authenticated.</summary>
    public static IWebAuthorizationRequirement Authenticated { get; } =
        new Assertion("authenticated", (c, _) => ValueTask.FromResult(c.Identity.IsAuthenticated));

    /// <summary>The user is in at least one of <paramref name="groups"/>.</summary>
    public static IWebAuthorizationRequirement AnyGroup(params string[] groups) =>
        new Assertion("any-group:" + string.Join(',', groups),
            (c, _) => ValueTask.FromResult(groups.Any(g => c.Identity.Groups.Contains(g, StringComparer.OrdinalIgnoreCase))));

    /// <summary>The user is in every one of <paramref name="groups"/>.</summary>
    public static IWebAuthorizationRequirement AllGroups(params string[] groups) =>
        new Assertion("all-groups:" + string.Join(',', groups),
            (c, _) => ValueTask.FromResult(groups.All(g => c.Identity.Groups.Contains(g, StringComparer.OrdinalIgnoreCase))));

    /// <summary>The user holds every one of <paramref name="permissions"/> (wildcards such as <c>posts.*</c> grant).</summary>
    public static IWebAuthorizationRequirement Permissions(params string[] permissions) =>
        new Assertion("permissions:" + string.Join(',', permissions), async (c, ct) =>
            WebPermissionMatcher.GrantsAll(await c.GetPermissionsAsync(ct).ConfigureAwait(false), permissions));

    /// <summary>The identity was established by one of <paramref name="schemes"/>.</summary>
    public static IWebAuthorizationRequirement Scheme(params string[] schemes) =>
        new Assertion("scheme:" + string.Join(',', schemes),
            (c, _) => ValueTask.FromResult(schemes.Contains(c.Identity.Scheme, StringComparer.OrdinalIgnoreCase)));

    /// <summary>The API key / token carries every one of <paramref name="scopes"/>.</summary>
    public static IWebAuthorizationRequirement Scopes(params string[] scopes) =>
        new Assertion("scopes:" + string.Join(',', scopes),
            (c, _) => ValueTask.FromResult(scopes.All(s => c.Identity.Scopes.Contains(s, StringComparer.OrdinalIgnoreCase))));

    /// <summary>The identity belongs to the request's tenant.</summary>
    public static IWebAuthorizationRequirement SameTenant { get; } =
        new Assertion("same-tenant", (c, _) => ValueTask.FromResult(c.Security.TenantId is null || string.Equals(c.Identity.TenantId, c.Security.TenantId, StringComparison.Ordinal)));

    /// <summary>A custom check.</summary>
    public static IWebAuthorizationRequirement Assert(string description, Func<WebAuthorizationContext, CancellationToken, ValueTask<bool>> check) =>
        new Assertion(description, check);

    private sealed class Assertion(string description, Func<WebAuthorizationContext, CancellationToken, ValueTask<bool>> check) : IWebAuthorizationRequirement
    {
        public string Description { get; } = description;

        public ValueTask<bool> IsSatisfiedAsync(WebAuthorizationContext context, CancellationToken cancellationToken) =>
            check(context, cancellationToken);
    }
}
