using System.Text.Json;
using CL.WebLogic.Routing.Models;
using CL.WebLogic.Runtime.Services;

namespace CL.WebLogic.Routing.Services;

/// <summary>
/// Passed to <see cref="IWebContributor.ContributeAsync"/>. Routes are staged here and committed
/// to the registry atomically when the contributor returns — a contributor that throws
/// registers nothing.
/// </summary>
public sealed class WebContributionBuilder : IWebRouteMapper
{
    private readonly List<WebRouteBuilder> _staged = [];
    private readonly List<Func<ValueTask>> _cleanups = [];
    private readonly List<Action> _commits = [];

    internal WebContributionBuilder(WebContributorDescriptor contributor, IServiceProvider services)
    {
        Contributor = contributor ?? throw new ArgumentNullException(nameof(contributor));
        Services = services ?? throw new ArgumentNullException(nameof(services));
        JsonOptions = WebJson.CreateOptions();
    }

    /// <summary>Who is contributing.</summary>
    public WebContributorDescriptor Contributor { get; }

    /// <summary>The application's service provider (resolve app services for handlers here).</summary>
    public IServiceProvider Services { get; }

    /// <summary>
    /// JSON options used by this contributor's JSON results. Owned per contributor so an unloaded
    /// plugin's types are never kept alive by a shared serializer cache.
    /// </summary>
    public JsonSerializerOptions JsonOptions { get; }

    /// <summary>Routes staged so far.</summary>
    public IReadOnlyList<WebRouteBuilder> StagedRoutes => _staged;

    /// <inheritdoc/>
    public WebRouteBuilder Map(WebRouteKind kind, string pattern, WebRouteHandler handler, params string[] methods) =>
        Stage(kind, kind == WebRouteKind.Fallback ? pattern : WebRoutePattern.Combine(string.Empty, pattern), handler, methods, null);

    /// <inheritdoc/>
    public WebRouteGroupBuilder MapGroup(string prefix) =>
        new(this, WebRoutePattern.Combine(string.Empty, prefix), null);

    /// <summary>
    /// Registers a cleanup that runs when the contributor is removed (plugin unload). Toolkit
    /// registries use this to drop widgets, providers and other non-route registrations.
    /// </summary>
    public void OnRemove(Func<ValueTask> cleanup)
    {
        ArgumentNullException.ThrowIfNull(cleanup);
        _cleanups.Add(cleanup);
    }

    /// <summary>
    /// Stages a non-route registration (widget, widget area, realtime bridge, …). It runs only when
    /// the whole contribution commits; pair it with <see cref="OnRemove"/> to undo it.
    /// </summary>
    public void OnCommit(Action commit)
    {
        ArgumentNullException.ThrowIfNull(commit);
        _commits.Add(commit);
    }

    internal IReadOnlyList<Action> Commits => _commits;

    internal IReadOnlyList<Func<ValueTask>> Cleanups => _cleanups;

    internal WebRouteBuilder Stage(WebRouteKind kind, string pattern, WebRouteHandler handler, string[] methods, WebRouteGroupBuilder? group)
    {
        ArgumentNullException.ThrowIfNull(handler);
        var route = new WebRouteBuilder(kind, pattern, WebRoutePattern.NormalizeMethods(methods), handler, group);
        _staged.Add(route);
        return route;
    }

    /// <summary>Freezes every staged route into a definition.</summary>
    internal IReadOnlyList<WebRouteDefinition> Build() =>
        _staged.Select(route =>
        {
            var settings = route.ResolveSettings();
            return new WebRouteDefinition
            {
                Metadata = WebRouteMetadata.From(route.Kind, route.Pattern, route.Methods, Contributor, settings),
                Handler = route.Handler,
                Middleware = [.. settings.Middleware],
                Order = route.Order,
                JsonOptions = JsonOptions
            };
        }).ToList();
}
