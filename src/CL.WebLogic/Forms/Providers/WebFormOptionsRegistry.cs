using System.Collections.Concurrent;

namespace CL.WebLogic.Forms.Providers;

/// <summary>Options providers by name: registered in DI, or added and removed at runtime (plugins).</summary>
public sealed class WebFormOptionsRegistry
{
    private readonly ConcurrentDictionary<string, IWebFormOptionsProvider> _providers = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Creates the registry with the DI-registered providers.</summary>
    public WebFormOptionsRegistry(IEnumerable<IWebFormOptionsProvider> providers)
    {
        foreach (var provider in providers ?? [])
            Register(provider);
    }

    /// <summary>Adds (or replaces) a provider.</summary>
    public void Register(IWebFormOptionsProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        _providers[provider.Name] = provider;
    }

    /// <summary>Removes a provider.</summary>
    public bool Unregister(string name) => _providers.TryRemove(name, out _);

    /// <summary>A provider by name.</summary>
    public IWebFormOptionsProvider? Get(string? name) => name is not null && _providers.TryGetValue(name, out var provider) ? provider : null;

    /// <summary>All providers.</summary>
    public IReadOnlyCollection<IWebFormOptionsProvider> All => [.. _providers.Values];
}
