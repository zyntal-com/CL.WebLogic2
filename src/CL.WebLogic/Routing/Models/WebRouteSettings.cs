namespace CL.WebLogic.Routing.Models;

/// <summary>
/// Mutable route options collected by the fluent builders. Group settings are copied into
/// each route first; route-level calls then override or extend them. Frozen into an
/// immutable <see cref="WebRouteMetadata"/> when the contribution is committed.
/// </summary>
public sealed class WebRouteSettings
{
    /// <summary>Route name (unique per contributor; used by the explorer and link generation).</summary>
    public string? Name { get; set; }

    /// <summary>Description shown in the explorer.</summary>
    public string? Description { get; set; }

    /// <summary>Free-form tags for grouping in the explorer.</summary>
    public List<string> Tags { get; } = [];

    /// <summary>
    /// <c>true</c> to allow anonymous callers, <c>false</c> to require authentication,
    /// <c>null</c> to inherit — routes are secure by default.
    /// </summary>
    public bool? AllowAnonymous { get; set; }

    /// <summary>Caller must be in at least one of these groups.</summary>
    public List<string> RequiredGroups { get; } = [];

    /// <summary>Caller must hold every one of these permissions.</summary>
    public List<string> RequiredPermissions { get; } = [];

    /// <summary>Named authorization policies that must all succeed.</summary>
    public List<string> Policies { get; } = [];

    /// <summary>Named CORS policy, or null for none.</summary>
    public string? CorsPolicy { get; set; }

    /// <summary>Named rate-limit policy, or null for the global limit only.</summary>
    public string? RateLimitPolicy { get; set; }

    /// <summary>Maximum request body size in bytes, or null for the toolkit default.</summary>
    public long? MaxBodyBytes { get; set; }

    /// <summary>Named output-cache policy, or null for no output caching.</summary>
    public string? OutputCachePolicy { get; set; }

    /// <summary>Allow unsafe methods from cross-site origins (skips the Origin / Sec-Fetch-Site check).</summary>
    public bool AllowCrossSitePost { get; set; }

    /// <summary>Route-scoped middleware, run in order after the security pipeline.</summary>
    public List<IWebMiddleware> Middleware { get; } = [];

    /// <summary>Sign-in page for this route or area (null = <c>Security.LoginPath</c>).</summary>
    public string? LoginPath { get; set; }

    /// <summary>Page signed-in users are sent to when access is denied (null = a 403 response).</summary>
    public string? AccessDeniedPath { get; set; }

    /// <summary>Step-up: how recently the user must have proven their credentials (null = no requirement).</summary>
    public TimeSpan? RecentSignInMaxAge { get; set; }

    /// <summary>Methods the recent sign-in must have used (with <see cref="RecentSignInMaxAge"/>).</summary>
    public List<string> RecentSignInMethods { get; } = [];

    /// <summary>Extra Content-Security-Policy sources for these routes.</summary>
    public Security.Models.WebCspAdditions Csp { get; } = new();

    /// <summary>Copies every value from <paramref name="source"/> into this instance (used for group inheritance).</summary>
    public void InheritFrom(WebRouteSettings source)
    {
        ArgumentNullException.ThrowIfNull(source);
        AllowAnonymous ??= source.AllowAnonymous;
        CorsPolicy ??= source.CorsPolicy;
        RateLimitPolicy ??= source.RateLimitPolicy;
        MaxBodyBytes ??= source.MaxBodyBytes;
        OutputCachePolicy ??= source.OutputCachePolicy;
        AllowCrossSitePost |= source.AllowCrossSitePost;
        Tags.InsertRange(0, source.Tags.Except(Tags, StringComparer.OrdinalIgnoreCase));
        RequiredGroups.InsertRange(0, source.RequiredGroups.Except(RequiredGroups, StringComparer.OrdinalIgnoreCase));
        RequiredPermissions.InsertRange(0, source.RequiredPermissions.Except(RequiredPermissions, StringComparer.OrdinalIgnoreCase));
        Policies.InsertRange(0, source.Policies.Except(Policies, StringComparer.Ordinal));
        Middleware.InsertRange(0, source.Middleware);
        LoginPath ??= source.LoginPath;
        AccessDeniedPath ??= source.AccessDeniedPath;
        if (RecentSignInMaxAge is null && source.RecentSignInMaxAge is not null)
        {
            RecentSignInMaxAge = source.RecentSignInMaxAge;
            RecentSignInMethods.InsertRange(0, source.RecentSignInMethods.Except(RecentSignInMethods, StringComparer.OrdinalIgnoreCase));
        }
        Csp.MergeFrom(source.Csp);
    }
}
