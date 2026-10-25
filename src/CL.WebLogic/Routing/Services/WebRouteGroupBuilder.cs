using CL.WebLogic.Routing.Models;

namespace CL.WebLogic.Routing.Services;

/// <summary>
/// A prefixed group of routes. Options set on the group (anonymous access, groups, CORS,
/// rate limit, middleware…) apply to every route mapped in it, including nested groups.
/// </summary>
public sealed class WebRouteGroupBuilder : IWebRouteMapper, IWebRouteSettingsBuilder
{
    private readonly WebContributionBuilder _owner;

    internal WebRouteGroupBuilder(WebContributionBuilder owner, string prefix, WebRouteGroupBuilder? parent)
    {
        _owner = owner;
        Prefix = prefix;
        Parent = parent;
    }

    /// <summary>The full prefix of this group (parent prefixes included).</summary>
    public string Prefix { get; }

    /// <inheritdoc/>
    public WebRouteSettings Settings { get; } = new();

    internal WebRouteGroupBuilder? Parent { get; }

    /// <inheritdoc/>
    public WebRouteBuilder Map(WebRouteKind kind, string pattern, WebRouteHandler handler, params string[] methods)
    {
        if (kind == WebRouteKind.Fallback)
            throw new InvalidOperationException("A fallback route can only be mapped at the contribution root, not inside a group.");

        return _owner.Stage(kind, WebRoutePattern.Combine(Prefix, pattern), handler, methods, this);
    }

    /// <inheritdoc/>
    public WebRouteGroupBuilder MapGroup(string prefix) =>
        new(_owner, WebRoutePattern.Combine(Prefix, prefix), this);
}
