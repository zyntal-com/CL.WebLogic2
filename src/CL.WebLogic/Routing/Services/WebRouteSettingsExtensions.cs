namespace CL.WebLogic.Routing.Services;

/// <summary>
/// Fluent options shared by <see cref="WebRouteBuilder"/> and <see cref="WebRouteGroupBuilder"/>.
/// On a group they apply to every route mapped in it; a route can override or extend them.
/// </summary>
public static class WebRouteSettingsExtensions
{
    /// <summary>Sets the route name.</summary>
    public static T WithName<T>(this T builder, string name) where T : IWebRouteSettingsBuilder
    {
        builder.Settings.Name = name;
        return builder;
    }

    /// <summary>Sets the explorer description.</summary>
    public static T WithDescription<T>(this T builder, string description) where T : IWebRouteSettingsBuilder
    {
        builder.Settings.Description = description;
        return builder;
    }

    /// <summary>Adds explorer tags.</summary>
    public static T WithTags<T>(this T builder, params string[] tags) where T : IWebRouteSettingsBuilder
    {
        builder.Settings.Tags.AddRange(tags);
        return builder;
    }

    /// <summary>Allows anonymous callers. Every anonymous route is listed in the startup log and the explorer.</summary>
    public static T AllowAnonymous<T>(this T builder) where T : IWebRouteSettingsBuilder
    {
        builder.Settings.AllowAnonymous = true;
        return builder;
    }

    /// <summary>Requires an authenticated caller (the default; useful to override an anonymous group).</summary>
    public static T RequireAuthenticated<T>(this T builder) where T : IWebRouteSettingsBuilder
    {
        builder.Settings.AllowAnonymous = false;
        return builder;
    }

    /// <summary>Requires membership of at least one of <paramref name="groups"/>.</summary>
    public static T RequireGroups<T>(this T builder, params string[] groups) where T : IWebRouteSettingsBuilder
    {
        builder.Settings.AllowAnonymous = false;
        builder.Settings.RequiredGroups.AddRange(groups);
        return builder;
    }

    /// <summary>Requires every one of <paramref name="permissions"/> (wildcards resolved by the permission resolver).</summary>
    public static T RequirePermissions<T>(this T builder, params string[] permissions) where T : IWebRouteSettingsBuilder
    {
        builder.Settings.AllowAnonymous = false;
        builder.Settings.RequiredPermissions.AddRange(permissions);
        return builder;
    }

    /// <summary>Requires the named authorization policy.</summary>
    public static T RequirePolicy<T>(this T builder, string policy) where T : IWebRouteSettingsBuilder
    {
        builder.Settings.Policies.Add(policy);
        return builder;
    }

    /// <summary>Applies the named CORS policy.</summary>
    public static T WithCors<T>(this T builder, string policy) where T : IWebRouteSettingsBuilder
    {
        builder.Settings.CorsPolicy = policy;
        return builder;
    }

    /// <summary>Applies the named rate-limit policy (in addition to the global limit).</summary>
    public static T WithRateLimit<T>(this T builder, string policy) where T : IWebRouteSettingsBuilder
    {
        builder.Settings.RateLimitPolicy = policy;
        return builder;
    }

    /// <summary>Caps the request body size.</summary>
    public static T WithBodyLimit<T>(this T builder, long maxBytes) where T : IWebRouteSettingsBuilder
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maxBytes);
        builder.Settings.MaxBodyBytes = maxBytes;
        return builder;
    }

    /// <summary>Applies the named output-cache policy.</summary>
    public static T WithOutputCache<T>(this T builder, string policy) where T : IWebRouteSettingsBuilder
    {
        builder.Settings.OutputCachePolicy = policy;
        return builder;
    }

    /// <summary>Accepts unsafe methods from cross-site origins (e.g. a public webhook). CSRF tokens still apply to cookie sessions.</summary>
    public static T AllowCrossSitePost<T>(this T builder) where T : IWebRouteSettingsBuilder
    {
        builder.Settings.AllowCrossSitePost = true;
        return builder;
    }

    /// <summary>Adds route-scoped middleware.</summary>
    public static T WithMiddleware<T>(this T builder, IWebMiddleware middleware) where T : IWebRouteSettingsBuilder
    {
        ArgumentNullException.ThrowIfNull(middleware);
        builder.Settings.Middleware.Add(middleware);
        return builder;
    }
}
