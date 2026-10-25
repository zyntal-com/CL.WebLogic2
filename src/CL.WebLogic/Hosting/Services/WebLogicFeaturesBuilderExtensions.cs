using CL.WebLogic.Caching.Services;
using CL.WebLogic.Dashboard.Services;
using CL.WebLogic.Realtime.Models;
using CL.WebLogic.Realtime.Services;
using CL.WebLogic.Widgets.Services;
using CodeLogic.Core.Events;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CL.WebLogic.Hosting.Services;

/// <summary>Widgets, dashboards, realtime and caching configuration on <see cref="WebLogicBuilder"/>.</summary>
public static class WebLogicFeaturesBuilderExtensions
{
    /// <summary>Stores widget instance settings in the app's store.</summary>
    public static WebLogicBuilder UseWidgetSettingsStore<TStore>(this WebLogicBuilder builder) where TStore : class, IWebWidgetSettingsStore =>
        Replace<IWebWidgetSettingsStore, TStore>(builder);

    /// <summary>Stores dashboard layouts in the app's store.</summary>
    public static WebLogicBuilder UseDashboardStore<TStore>(this WebLogicBuilder builder) where TStore : class, IWebDashboardLayoutStore =>
        Replace<IWebDashboardLayoutStore, TStore>(builder);

    /// <summary>Decides whose dashboard a request uses (default: the signed-in user).</summary>
    public static WebLogicBuilder UseDashboardOwnerResolver<TResolver>(this WebLogicBuilder builder) where TResolver : class, IWebDashboardOwnerResolver =>
        Replace<IWebDashboardOwnerResolver, TResolver>(builder);

    /// <summary>Supplies default dashboard layouts.</summary>
    public static WebLogicBuilder UseDashboardDefaults<TDefaults>(this WebLogicBuilder builder) where TDefaults : class, IWebDashboardDefaults =>
        Replace<IWebDashboardDefaults, TDefaults>(builder);

    /// <summary>Carries realtime messages between nodes (required for clusters).</summary>
    public static WebLogicBuilder UseRealtimeBackplane<TBackplane>(this WebLogicBuilder builder) where TBackplane : class, IWebRealtimeBackplane =>
        Replace<IWebRealtimeBackplane, TBackplane>(builder);

    /// <summary>Handles messages clients send over WebSockets.</summary>
    public static WebLogicBuilder UseRealtimeReceiver<TReceiver>(this WebLogicBuilder builder) where TReceiver : class, IWebRealtimeReceiver =>
        Replace<IWebRealtimeReceiver, TReceiver>(builder);

    /// <summary>Publishes every <typeparamref name="TEvent"/> to realtime clients.</summary>
    public static WebLogicBuilder BridgeEvent<TEvent>(this WebLogicBuilder builder, string type, Func<TEvent, WebRealtimeAudience> audience, Func<TEvent, object?> data, Func<TEvent, string?>? channel = null)
        where TEvent : IEvent
    {
        ArgumentNullException.ThrowIfNull(builder);
        if (!WebRealtimeHub.IsValidName(type))
            throw new ArgumentException($"Realtime type '{type}' must match ^[\\w.:-]{{1,100}}$.", nameof(type));
        builder.Services.AddSingleton(new WebRealtimeBridgeRegistration(bridge => bridge.Register(type, audience, data, channel)));
        return builder;
    }

    /// <summary>
    /// Adds a named output-cache policy for <c>.WithOutputCache(name)</c>. WebLogic's safety policy
    /// (anonymous GET/HEAD only, nothing stored with cookies, CSRF tokens or session writes) runs first.
    /// </summary>
    public static WebLogicBuilder AddOutputCachePolicy(this WebLogicBuilder builder, string name, Action<OutputCachePolicyBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(configure);
        builder.Services.Configure<OutputCacheOptions>(options => options.AddPolicy(name, policy =>
        {
            policy.AddPolicy<WebLogicOutputCachePolicy>();
            configure(policy);
        }));
        return builder;
    }

    /// <summary>Replaces the fragment cache (e.g. a distributed one).</summary>
    public static WebLogicBuilder UseFragmentCache<TCache>(this WebLogicBuilder builder) where TCache : class, IWebFragmentCache =>
        Replace<IWebFragmentCache, TCache>(builder);

    private static WebLogicBuilder Replace<TContract, TImplementation>(WebLogicBuilder builder)
        where TContract : class
        where TImplementation : class, TContract
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Services.Replace(ServiceDescriptor.Singleton<TContract, TImplementation>());
        return builder;
    }
}
