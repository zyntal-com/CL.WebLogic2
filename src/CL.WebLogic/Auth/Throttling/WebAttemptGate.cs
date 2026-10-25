using System.Collections.Concurrent;

namespace CL.WebLogic.Auth.Throttling;

/// <summary>
/// Lets one credential attempt per key (login name, user awaiting a second factor) run at a time
/// on this node. The throttle and lockout are check-then-record, so without this a burst of
/// parallel guesses would all pass the check before the first failure is counted. A concurrent
/// attempt for the same key is refused as throttled. Memory is bounded by the attempts in flight.
/// </summary>
public sealed class WebAttemptGate
{
    private readonly ConcurrentDictionary<string, byte> _inFlight = new(StringComparer.Ordinal);

    /// <summary>Enters the gate for <paramref name="key"/>; null when another attempt holds it.</summary>
    public IDisposable? TryEnter(string key)
    {
        var normalized = (key ?? string.Empty).Trim().ToUpperInvariant();
        return _inFlight.TryAdd(normalized, 0) ? new Lease(this, normalized) : null;
    }

    private sealed class Lease(WebAttemptGate gate, string key) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
                gate._inFlight.TryRemove(key, out _);
        }
    }
}
