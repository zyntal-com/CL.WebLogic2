using CL.WebLogic.Realtime.Models;
using CL.WebLogic.Routing.Services;
using CodeLogic.Core.Events;
using Microsoft.Extensions.DependencyInjection;

namespace CL.WebLogic.Realtime.Services;

/// <summary>Event bridges from contributors and plugins (removed with the contribution).</summary>
public static class WebRealtimeContributionExtensions
{
    /// <summary>Publishes every <typeparamref name="TEvent"/> to realtime clients while the contribution is registered.</summary>
    public static void BridgeEvent<TEvent>(this WebContributionBuilder builder, string type, Func<TEvent, WebRealtimeAudience> audience, Func<TEvent, object?> data, Func<TEvent, string?>? channel = null)
        where TEvent : IEvent
    {
        ArgumentNullException.ThrowIfNull(builder);
        var bridge = builder.Services.GetRequiredService<WebRealtimeBridge>();
        IDisposable? subscription = null;
        builder.OnCommit(() => subscription = bridge.Register(type, audience, data, channel));
        builder.OnRemove(() =>
        {
            subscription?.Dispose();
            return ValueTask.CompletedTask;
        });
    }
}
