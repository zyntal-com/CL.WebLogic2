using CL.WebLogic.Configuration.Services;
using CL.WebLogic.Forms.Providers;
using CL.WebLogic.Forms.Services;
using CL.WebLogic.Realtime.Services;
using CL.WebLogic.Routing.Services;
using CL.WebLogic.Security.Pipeline;
using CL.WebLogic.Widgets.Services;
using Microsoft.Extensions.Hosting;

namespace CL.WebLogic.Explorer.Services;

/// <summary>Builds the explorer document: routes and their protection, the security pipeline, widgets, forms, realtime and contributors.</summary>
public sealed class ExplorerModelBuilder
{
    private readonly WebRouteRegistry _routes;
    private readonly ContributorRegistrar _contributors;
    private readonly IEnumerable<IWebSecurityStage> _stages;
    private readonly WebWidgetRegistry _widgets;
    private readonly WebFormRegistry _forms;
    private readonly WebFormDefinitionProvider _definitions;
    private readonly WebFormOptionsRegistry _options;
    private readonly WebRealtimeHub _realtime;
    private readonly WebLogicConfigHolder _config;
    private readonly ConfigApplyCoordinator? _coordinator;
    private readonly IHostEnvironment _environment;

    /// <summary>Creates the builder.</summary>
    public ExplorerModelBuilder(WebRouteRegistry routes, ContributorRegistrar contributors, IEnumerable<IWebSecurityStage> stages, WebWidgetRegistry widgets,
        WebFormRegistry forms, WebFormDefinitionProvider definitions, WebFormOptionsRegistry options, WebRealtimeHub realtime,
        WebLogicConfigHolder config, IHostEnvironment environment, ConfigApplyCoordinator? coordinator = null)
    {
        _routes = routes;
        _contributors = contributors;
        _stages = stages;
        _widgets = widgets;
        _forms = forms;
        _definitions = definitions;
        _options = options;
        _realtime = realtime;
        _config = config;
        _coordinator = coordinator;
        _environment = environment;
    }

    /// <summary>The explorer document (serialize with the default JSON options).</summary>
    public object Build()
    {
        var config = _config.Current;
        var routes = _routes.Routes.Select(r => r.Metadata).OrderBy(m => m.Pattern, StringComparer.Ordinal).ThenBy(m => string.Join(',', m.Methods), StringComparer.Ordinal).ToList();
        var disabled = config.Security.DisabledStages.ToHashSet(StringComparer.OrdinalIgnoreCase);

        return new
        {
            site = config.SiteName,
            environment = _environment.EnvironmentName,
            version = CL.Internal.InternalLibraryVersion.Current,
            restartPending = _coordinator?.RestartPending ?? false,
            routes = routes.Select(m => new
            {
                pattern = m.Pattern,
                methods = m.Methods,
                kind = m.Kind.ToString(),
                name = m.Name,
                description = m.Description,
                tags = m.Tags,
                contributor = m.Contributor.Id,
                anonymous = m.AllowAnonymous,
                groups = m.RequiredGroups,
                permissions = m.RequiredPermissions,
                policies = m.Policies,
                rateLimit = m.RateLimitPolicy,
                cors = m.CorsPolicy,
                outputCache = m.OutputCachePolicy,
                maxBodyBytes = m.MaxBodyBytes,
                crossSitePost = m.AllowCrossSitePost,
                loginPath = m.LoginPath,
                accessDeniedPath = m.AccessDeniedPath,
                recentSignIn = m.RecentSignIn?.Description,
                csp = m.CspAdditions
            }),
            anonymousRoutes = routes.Where(m => m.AllowAnonymous).Select(m => string.Join(',', m.Methods) + " " + m.Pattern),
            stages = _stages.OrderBy(s => s.Phase).ThenBy(s => s.Order).Select(s => new
            {
                name = s.Name,
                phase = s.Phase.ToString(),
                order = s.Order,
                enabled = !disabled.Contains(s.Name)
            }),
            widgets = _widgets.Widgets.OrderBy(w => w.Name, StringComparer.Ordinal).Select(w => new
            {
                name = w.Name,
                description = w.Description,
                tags = w.Tags,
                contributor = w.Contributor.Id,
                anonymous = w.Access.AllowAnonymous,
                groups = w.Access.Groups,
                permissions = w.Access.Permissions,
                policy = w.Access.Policy,
                actions = w.Actions.Keys,
                dashboard = w.Dashboard,
                refreshSeconds = w.RefreshSeconds,
                sampleParameters = w.SampleParameters
            }),
            widgetAreas = _widgets.Placements.GroupBy(p => p.Area, StringComparer.OrdinalIgnoreCase).OrderBy(g => g.Key, StringComparer.Ordinal).Select(g => new
            {
                area = g.Key,
                placements = g.OrderBy(p => p.Order).Select(p => new { widget = p.WidgetName, instance = p.InstanceId, order = p.Order, include = p.IncludePaths, exclude = p.ExcludePaths, contributor = p.Contributor.Id })
            }),
            forms = _forms.All.OrderBy(f => f.Key, StringComparer.Ordinal).Select(f =>
            {
                var definition = _definitions.Get(f.Value);
                return new
                {
                    id = f.Key,
                    model = f.Value.FullName,
                    steps = definition.Steps.Select(s => s.Name),
                    antiBot = definition.AntiBot,
                    fields = definition.Fields.Select(x => new { name = x.Name, type = x.Type.ToString(), required = x.Required, options = x.OptionsProvider, remote = x.Remote })
                };
            }),
            optionsProviders = _options.All.OrderBy(p => p.Name, StringComparer.Ordinal).Select(p => new { name = p.Name, anonymous = p.AllowAnonymous, policy = p.Policy }),
            realtime = new
            {
                enabled = config.Realtime.Enabled,
                webSockets = config.Realtime.WebSockets,
                allowAnonymous = config.Realtime.AllowAnonymous,
                connections = _realtime.ConnectionCount,
                replayBuffered = _realtime.ReplayCount,
                broadcastRequests = config.Realtime.BroadcastRequests
            },
            contributors = _contributors.Contributors.OrderBy(c => c.Id, StringComparer.Ordinal).Select(c => new { id = c.Id, name = c.Name, kind = c.Kind, description = c.Description })
        };
    }
}
