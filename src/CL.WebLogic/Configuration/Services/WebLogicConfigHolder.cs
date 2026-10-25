using CL.WebLogic.Configuration.Models;

namespace CL.WebLogic.Configuration.Services;

/// <summary>
/// Holds the current <see cref="WebLogicConfig"/>. The instance is swapped atomically when
/// CodeLogic applies a config change, so every service reads <see cref="Current"/> per use
/// instead of capturing a copy at startup.
/// </summary>
public sealed class WebLogicConfigHolder
{
    private WebLogicConfig _current;
    private long _revision;

    /// <summary>Creates a holder seeded with the initial config.</summary>
    public WebLogicConfigHolder(WebLogicConfig initial)
    {
        ArgumentNullException.ThrowIfNull(initial);
        _current = initial;
    }

    /// <summary>The config in effect right now.</summary>
    public WebLogicConfig Current => Volatile.Read(ref _current);

    /// <summary>Incremented on every swap; lets caches detect that the config changed.</summary>
    public long Revision => Interlocked.Read(ref _revision);

    /// <summary>Raised after <see cref="Swap"/> with the previous and the new config.</summary>
    public event Action<WebLogicConfig, WebLogicConfig>? Changed;

    /// <summary>Atomically replaces the current config and notifies subscribers.</summary>
    public void Swap(WebLogicConfig next)
    {
        ArgumentNullException.ThrowIfNull(next);
        var previous = Interlocked.Exchange(ref _current, next);
        Interlocked.Increment(ref _revision);
        Changed?.Invoke(previous, next);
    }
}
