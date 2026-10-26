namespace CL.WebLogic.Routing.Models;

/// <summary>
/// Immutable description of a WebLogic route. Attached to the ASP.NET endpoint as metadata,
/// where the security pipeline, explorer and output cache read it.
/// </summary>
public sealed class WebRouteMetadata
{
    /// <summary>What the route produces.</summary>
    public required WebRouteKind Kind { get; init; }

    /// <summary>The route pattern as registered (ASP.NET route template syntax).</summary>
    public required string Pattern { get; init; }

    /// <summary>Allowed HTTP methods (HEAD is implied for GET).</summary>
    public required IReadOnlyList<string> Methods { get; init; }

    /// <summary>Contributor that registered the route.</summary>
    public required WebContributorDescriptor Contributor { get; init; }

    /// <summary>Route name, or empty.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Description, or empty.</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>Explorer tags.</summary>
    public IReadOnlyList<string> Tags { get; init; } = [];

    /// <summary>True when anonymous callers are allowed. Defaults to false (secure by default).</summary>
    public bool AllowAnonymous { get; init; }

    /// <summary>Caller must be in at least one of these groups (empty = no group requirement).</summary>
    public IReadOnlyList<string> RequiredGroups { get; init; } = [];

    /// <summary>Caller must hold all of these permissions.</summary>
    public IReadOnlyList<string> RequiredPermissions { get; init; } = [];

    /// <summary>Named authorization policies that must all succeed.</summary>
    public IReadOnlyList<string> Policies { get; init; } = [];

    /// <summary>Named CORS policy, or null.</summary>
    public string? CorsPolicy { get; init; }

    /// <summary>Named rate-limit policy, or null.</summary>
    public string? RateLimitPolicy { get; init; }

    /// <summary>Maximum request body size in bytes, or null for the toolkit default.</summary>
    public long? MaxBodyBytes { get; init; }

    /// <summary>Named output-cache policy, or null.</summary>
    public string? OutputCachePolicy { get; init; }

    /// <summary>Unsafe methods may come from cross-site origins.</summary>
    public bool AllowCrossSitePost { get; init; }

    /// <summary>Layout for page templates that declare none, or null.</summary>
    public string? Layout { get; init; }

    /// <summary>Sign-in page for this route (null = <c>Security.LoginPath</c>).</summary>
    public string? LoginPath { get; init; }

    /// <summary>Where signed-in users go when access is denied (null = 403).</summary>
    public string? AccessDeniedPath { get; init; }

    /// <summary>Step-up requirement, or null.</summary>
    public Security.Authorization.RecentSignInRequirement? RecentSignIn { get; init; }

    /// <summary>Extra Content-Security-Policy sources per directive (empty = the site policy).</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> CspAdditions { get; init; } = new Dictionary<string, IReadOnlyList<string>>();

    /// <summary>Freezes builder settings into metadata.</summary>
    public static WebRouteMetadata From(
        WebRouteKind kind,
        string pattern,
        IReadOnlyList<string> methods,
        WebContributorDescriptor contributor,
        WebRouteSettings settings) => new()
    {
        Kind = kind,
        Pattern = pattern,
        Methods = methods,
        Contributor = contributor,
        Name = settings.Name ?? string.Empty,
        Description = settings.Description ?? string.Empty,
        Tags = [.. settings.Tags],
        AllowAnonymous = settings.AllowAnonymous ?? false,
        RequiredGroups = [.. settings.RequiredGroups],
        RequiredPermissions = [.. settings.RequiredPermissions],
        Policies = [.. settings.Policies],
        CorsPolicy = settings.CorsPolicy,
        RateLimitPolicy = settings.RateLimitPolicy,
        MaxBodyBytes = settings.MaxBodyBytes,
        OutputCachePolicy = settings.OutputCachePolicy,
        AllowCrossSitePost = settings.AllowCrossSitePost,
        LoginPath = settings.LoginPath,
        Layout = settings.Layout,
        AccessDeniedPath = settings.AccessDeniedPath,
        RecentSignIn = settings.RecentSignInMaxAge is { } maxAge ? new Security.Authorization.RecentSignInRequirement(maxAge, [.. settings.RecentSignInAlternatives]) : null,
        CspAdditions = settings.Csp.Sources
    };
}
