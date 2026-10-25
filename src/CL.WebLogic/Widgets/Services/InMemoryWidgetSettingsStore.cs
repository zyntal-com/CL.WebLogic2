using System.Collections.Concurrent;
using CL.WebLogic.Widgets.Models;

namespace CL.WebLogic.Widgets.Services;

/// <summary>Development default: settings in memory (lost on restart; a warning is logged outside Development).</summary>
public sealed class InMemoryWidgetSettingsStore : IWebWidgetSettingsStore, Security.Services.IWebDevelopmentStore
{
    private const int MaxInstances = 10000;
    private readonly ConcurrentDictionary<(string Owner, string Instance), WebWidgetSettings> _settings = new();

    /// <inheritdoc/>
    public ValueTask<WebWidgetSettings?> GetAsync(string ownerKey, string instanceId, CancellationToken cancellationToken) =>
        ValueTask.FromResult(_settings.GetValueOrDefault((ownerKey, instanceId)));

    /// <inheritdoc/>
    public ValueTask SaveAsync(WebWidgetSettings settings, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var key = (settings.OwnerKey, settings.InstanceId);
        if (_settings.Count >= MaxInstances && !_settings.ContainsKey(key))
            throw new InvalidOperationException("The in-memory widget settings store is full; supply a real IWebWidgetSettingsStore.");
        _settings[key] = settings with { Values = new Dictionary<string, string>(settings.Values, StringComparer.OrdinalIgnoreCase) };
        return ValueTask.CompletedTask;
    }
}
