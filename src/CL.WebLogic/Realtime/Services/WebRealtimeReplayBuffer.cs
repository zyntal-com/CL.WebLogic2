using CL.WebLogic.Realtime.Models;

namespace CL.WebLogic.Realtime.Services;

/// <summary>Recent messages for Last-Event-ID resume, bounded by count and age.</summary>
public sealed class WebRealtimeReplayBuffer
{
    private readonly LinkedList<WebRealtimeEnvelope> _items = new();
    private readonly Dictionary<string, LinkedListNode<WebRealtimeEnvelope>> _byId = new(StringComparer.Ordinal);
    private readonly Lock _lock = new();

    /// <summary>Adds a message and trims to <paramref name="maxCount"/> and <paramref name="maxAge"/>.</summary>
    public void Add(WebRealtimeEnvelope envelope, int maxCount, TimeSpan maxAge, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        lock (_lock)
        {
            if (maxCount > 0 && !_byId.ContainsKey(envelope.Id))
                _byId[envelope.Id] = _items.AddLast(envelope);
            while (_items.First is { } first && (_items.Count > maxCount || now - first.Value.CreatedAt > maxAge))
            {
                _byId.Remove(first.Value.Id);
                _items.RemoveFirst();
            }
        }
    }

    /// <summary>
    /// Messages after <paramref name="lastEventId"/>; <c>Found</c> is false when the id is no longer
    /// buffered (the client missed messages and should resynchronize).
    /// </summary>
    public (bool Found, IReadOnlyList<WebRealtimeEnvelope> Messages) After(string lastEventId)
    {
        lock (_lock)
        {
            if (!_byId.TryGetValue(lastEventId, out var node))
                return (false, []);
            var result = new List<WebRealtimeEnvelope>();
            for (var next = node.Next; next is not null; next = next.Next)
                result.Add(next.Value);
            return (true, result);
        }
    }

    /// <summary>Buffered message count.</summary>
    public int Count
    {
        get
        {
            lock (_lock)
                return _items.Count;
        }
    }
}
