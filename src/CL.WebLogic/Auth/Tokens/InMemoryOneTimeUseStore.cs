using CL.WebLogic.Security.Services;

namespace CL.WebLogic.Auth.Tokens;

/// <summary>Per-node one-time-use store on a bounded cache.</summary>
public sealed class InMemoryOneTimeUseStore : IWebOneTimeUseStore, Security.Services.IWebNodeLocalStore
{
    private readonly BoundedTtlCache<byte> _used;
    private readonly TimeProvider _time;
    private readonly object _lock = new();

    /// <summary>Creates the store.</summary>
    public InMemoryOneTimeUseStore(TimeProvider? time = null)
    {
        _time = time ?? TimeProvider.System;
        _used = new BoundedTtlCache<byte>(200_000, _time);
    }

    /// <inheritdoc/>
    public ValueTask<bool> TryUseAsync(string key, DateTimeOffset expiresAt, CancellationToken cancellationToken)
    {
        var ttl = expiresAt - _time.GetUtcNow();
        if (ttl <= TimeSpan.Zero)
            return ValueTask.FromResult(false);
        lock (_lock)
        {
            if (_used.TryGet(key, out _))
                return ValueTask.FromResult(false);
            _used.Set(key, 0, ttl);
            return ValueTask.FromResult(true);
        }
    }
}
