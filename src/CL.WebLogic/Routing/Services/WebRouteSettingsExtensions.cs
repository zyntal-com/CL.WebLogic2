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

    /// <summary>Requires the named authorization policy (and so a signed-in user).</summary>
    public static T RequirePolicy<T>(this T builder, string policy) where T : IWebRouteSettingsBuilder
    {
        builder.Settings.AllowAnonymous = false;
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

    /// <summary>
    /// Adds a strong ETag (SHA-256 of the body) to non-personal 200 GET/HEAD responses and answers a
    /// matching <c>If-None-Match</c> with 304. Overrides <c>Cache.ETags</c>.
    /// </summary>
    public static T WithETag<T>(this T builder) where T : IWebRouteSettingsBuilder
    {
        builder.Settings.ETag = true;
        return builder;
    }

    /// <summary>No computed ETag for these routes, even when <c>Cache.ETags</c> is on (e.g. streaming or large responses).</summary>
    public static T WithoutETag<T>(this T builder) where T : IWebRouteSettingsBuilder
    {
        builder.Settings.ETag = false;
        return builder;
    }

    /// <summary>Never compresses responses of these routes, even when <c>Compression.Enabled</c> is on.</summary>
    public static T WithoutCompression<T>(this T builder) where T : IWebRouteSettingsBuilder
    {
        builder.Settings.Compression = false;
        return builder;
    }

    /// <summary>
    /// Compresses these routes even when the response is personal (signed in, session, CSRF token,
    /// cookies). Only when <c>Compression.Enabled</c> is on. Use it only for responses where no
    /// attacker-controlled input is reflected next to a secret: compressed sizes can reveal secrets
    /// (BREACH).
    /// </summary>
    public static T AllowPersonalCompression<T>(this T builder) where T : IWebRouteSettingsBuilder
    {
        builder.Settings.Compression = true;
        return builder;
    }

    /// <summary>Accepts unsafe methods from cross-site origins (e.g. a public webhook). CSRF tokens still apply to cookie sessions.</summary>
    public static T AllowCrossSitePost<T>(this T builder) where T : IWebRouteSettingsBuilder
    {
        builder.Settings.AllowCrossSitePost = true;
        return builder;
    }

    /// <summary>
    /// Default layout for page templates of these routes that declare no <c>{layout:…}</c>, e.g.
    /// <c>MapGroup("/admin").WithLayout("admin/layout")</c>. A template's own layout still wins, and
    /// the theme can override the layout file like any template.
    /// </summary>
    public static T WithLayout<T>(this T builder, string layoutPath) where T : IWebRouteSettingsBuilder
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(layoutPath);
        if (layoutPath.Contains("..", StringComparison.Ordinal) || layoutPath.Contains('\\') ||layoutPath.Contains(':'))
            throw new ArgumentException($"'{layoutPath}' is not a valid template path.", nameof(layoutPath));
        builder.Settings.Layout = layoutPath.TrimStart('/');
        return builder;
    }

    /// <summary>
    /// Sends unauthenticated visitors of these pages to <paramref name="path"/> instead of
    /// <c>Security.LoginPath</c> (e.g. an admin area with its own sign-in page). Local paths only.
    /// </summary>
    public static T WithLoginPath<T>(this T builder, string path) where T : IWebRouteSettingsBuilder
    {
        builder.Settings.LoginPath = LocalPath(path, nameof(path));
        return builder;
    }

    /// <summary>Sends signed-in visitors who are not allowed to <paramref name="path"/> (with <c>returnUrl</c>) instead of answering 403. Pages only.</summary>
    public static T WithAccessDeniedPath<T>(this T builder, string path) where T : IWebRouteSettingsBuilder
    {
        builder.Settings.AccessDeniedPath = LocalPath(path, nameof(path));
        return builder;
    }

    /// <summary>
    /// Step-up: the user must have proven their credentials within <paramref name="maxAge"/>, with
    /// every one of <paramref name="methods"/> (e.g. <c>pwd</c>, <c>otp</c>). Otherwise pages
    /// redirect to the login path with <c>reauth=1</c> and APIs answer 401. Users prove it again with
    /// <c>request.Auth.ReauthenticateAsync</c> (or any new sign-in).
    /// </summary>
    public static T RequireRecentSignIn<T>(this T builder, TimeSpan maxAge, params string[] methods) where T : IWebRouteSettingsBuilder =>
        builder.RequireRecentSignInAnyOf(maxAge, methods is { Length: > 0 } ? [methods] : []);

    /// <summary>
    /// Step-up with alternatives: the user must have proven their credentials within
    /// <paramref name="maxAge"/> using every method of at least one set, e.g.
    /// <c>RequireRecentSignInAnyOf(TimeSpan.FromHours(8), ["pwd", "otp"], ["passkey"])</c> accepts a
    /// password with an authenticator code, or a passkey. No sets = any method.
    /// </summary>
    public static T RequireRecentSignInAnyOf<T>(this T builder, TimeSpan maxAge, params string[][] alternatives) where T : IWebRouteSettingsBuilder
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(maxAge, TimeSpan.Zero);
        ArgumentNullException.ThrowIfNull(alternatives);
        if (alternatives.Any(a => a is null || a.Length == 0 || a.Any(string.IsNullOrWhiteSpace)))
            throw new ArgumentException("Each alternative needs at least one method name.", nameof(alternatives));
        builder.Settings.AllowAnonymous = false;
        builder.Settings.RecentSignInMaxAge = maxAge;
        builder.Settings.RecentSignInAlternatives.Clear();
        builder.Settings.RecentSignInAlternatives.AddRange(alternatives);
        return builder;
    }

    /// <summary>
    /// Adds Content-Security-Policy sources for these routes only, e.g.
    /// <c>.WithCsp(c =&gt; c.AddFrameSource("https://www.youtube-nocookie.com"))</c>.
    /// </summary>
    public static T WithCsp<T>(this T builder, Action<Security.Models.WebCspAdditions> configure) where T : IWebRouteSettingsBuilder
    {
        ArgumentNullException.ThrowIfNull(configure);
        configure(builder.Settings.Csp);
        return builder;
    }

    /// <summary>Adds route-scoped middleware.</summary>
    public static T WithMiddleware<T>(this T builder, IWebMiddleware middleware) where T : IWebRouteSettingsBuilder
    {
        ArgumentNullException.ThrowIfNull(middleware);
        builder.Settings.Middleware.Add(middleware);
        return builder;
    }

    private static string LocalPath(string path, string parameter)
    {
        if (!Runtime.Services.WebUrl.IsLocal(path) || path.Contains('#', StringComparison.Ordinal))
            throw new ArgumentException($"'{path}' must be a local path such as /admin/login.", parameter);
        return path;
    }
}
