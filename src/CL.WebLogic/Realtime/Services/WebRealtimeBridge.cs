using CL.WebLogic.Hosting.Services;
using CL.WebLogic.Realtime.Models;
using CodeLogic.Core.Events;

namespace CL.WebLogic.Realtime.Services;

/// <summary>
/// Forwards CodeLogic events to realtime clients. The data mapping is explicit so events never
/// leak fields by accident; the audience decides who receives each message.
/// </summary>
public sealed class WebRealtimeBridge
{
    private readonly WebRealtimeHub _hub;
    private readonly IWebLogicContext _context;

    /// <summary>Creates the bridge.</summary>
    public WebRealtimeBridge(WebRealtimeHub hub, IWebLogicContext context)
    {
        _hub = hub ?? throw new ArgumentNullException(nameof(hub));
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <summary>Publishes every <typeparamref name="TEvent"/> as <paramref name="type"/>; dispose the result to stop.</summary>
    public IDisposable Register<TEvent>(string type, Func<TEvent, WebRealtimeAudience> audience, Func<TEvent, object?> data, Func<TEvent, string?>? channel = null)
        where TEvent : IEvent
    {
        ArgumentNullException.ThrowIfNull(audience);
        ArgumentNullException.ThrowIfNull(data);
        if (!WebRealtimeHub.IsValidName(type))
            throw new ArgumentException($"Realtime type '{type}' must match ^[\\w.:-]{{1,100}}$.", nameof(type));

        return _context.Events.SubscribeAsync<TEvent>(async e =>
        {
            try
            {
                await _hub.PublishAsync(type, data(e), audience(e), channel?.Invoke(e)).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _context.Logger.Error($"Realtime bridge for {typeof(TEvent).Name} failed: {ex.Message}", ex);
            }
        });
    }
}
