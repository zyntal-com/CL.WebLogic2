using CL.WebLogic.Realtime.Models;

namespace CL.WebLogic.Realtime.Services;

/// <summary>Single-node backplane: delivers straight to this process's subscribers.</summary>
public sealed class InProcessRealtimeBackplane : IWebRealtimeBackplane, Security.Services.IWebNodeLocalStore
{
    private readonly Lock _lock = new();
    private Func<WebRealtimeEnvelope, ValueTask>[] _handlers = [];

    /// <inheritdoc/>
    public async Task PublishAsync(WebRealtimeEnvelope envelope, CancellationToken cancellationToken)
    {
        foreach (var handler in Volatile.Read(ref _handlers))
            await handler(envelope).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public IDisposable Subscribe(Func<WebRealtimeEnvelope, ValueTask> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        lock (_lock)
            Volatile.Write(ref _handlers, [.. _handlers, handler]);
        return new Subscription(this, handler);
    }

    private void Unsubscribe(Func<WebRealtimeEnvelope, ValueTask> handler)
    {
        lock (_lock)
            Volatile.Write(ref _handlers, [.. _handlers.Where(h => h != handler)]);
    }

    private sealed class Subscription(InProcessRealtimeBackplane owner, Func<WebRealtimeEnvelope, ValueTask> handler) : IDisposable
    {
        public void Dispose() => owner.Unsubscribe(handler);
    }
}
