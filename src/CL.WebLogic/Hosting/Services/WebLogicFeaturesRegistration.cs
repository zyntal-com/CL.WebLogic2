using CL.WebLogic.Caching.Services;
using CL.WebLogic.Configuration.Services;
using CL.WebLogic.Dashboard.Services;
using CL.WebLogic.Explorer.Services;
using CL.WebLogic.Realtime.Services;
using CL.WebLogic.Security.Authorization;
using CL.WebLogic.Templates.Rendering;
using CL.WebLogic.Widgets.Services;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CL.WebLogic.Hosting.Services;

/// <summary>Registers widgets, dashboards, realtime, caching and the explorer (called by <c>AddCodeLogicWebLogic</c>).</summary>
internal static class WebLogicFeaturesRegistration
{
    /// <summary>Name of the built-in output-cache policy.</summary>
    public const string DefaultOutputCachePolicy = "default";

    /// <summary>Adds the services.</summary>
    public static void AddFeatures(IServiceCollection services)
    {
        services.TryAddSingleton<WebAccessEvaluator>();

        // Widgets and dashboards: tools in the toolkit, persistence from the app.
        services.TryAddSingleton<WebWidgetRegistry>();
        services.TryAddSingleton<IWebWidgetSettingsStore, InMemoryWidgetSettingsStore>();
        services.TryAddSingleton<WebWidgetRenderer>();
        services.TryAddSingleton<ITemplateWidgetRenderer>(sp => sp.GetRequiredService<WebWidgetRenderer>());
        services.TryAddSingleton<WidgetsContributor>();
        services.TryAddSingleton<IWebDashboardLayoutStore, InMemoryDashboardLayoutStore>();
        services.TryAddSingleton<IWebDashboardOwnerResolver, UserDashboardOwnerResolver>();
        services.TryAddSingleton<IWebDashboardDefaults, EmptyDashboardDefaults>();
        services.TryAddSingleton<WebDashboardService>();
        services.TryAddSingleton<DashboardContributor>();

        // Realtime.
        services.TryAddSingleton<IWebRealtimeBackplane, InProcessRealtimeBackplane>();
        services.TryAddSingleton<WebRealtimeHub>();
        services.TryAddSingleton<WebRealtimeTickets>();
        services.TryAddSingleton<WebRealtimeIdentityFactory>();
        services.TryAddSingleton<WebRealtimeBridge>();
        services.TryAddSingleton<RealtimeContributor>();
        services.AddHostedService<RealtimeStartupService>();
        services.AddHostedService<RealtimeSessionMonitor>();

        // Caching: the shared-framework output cache with WebLogic's safety policy in front.
        services.AddOutputCache();
        services.AddOptions<OutputCacheOptions>().Configure<WebLogicConfigHolder>((options, config) =>
            options.AddPolicy(DefaultOutputCachePolicy, builder => builder
                .AddPolicy<WebLogicOutputCachePolicy>()
                .Expire(TimeSpan.FromSeconds(config.Current.Cache.DefaultOutputSeconds))));
        services.TryAddSingleton<IWebFragmentCache, MemoryFragmentCache>();
        services.TryAddSingleton<IWebCacheInvalidator, WebCacheInvalidator>();

        // Explorer.
        services.TryAddSingleton<ExplorerModelBuilder>();
        services.TryAddSingleton<ExplorerContributor>();
    }
}
