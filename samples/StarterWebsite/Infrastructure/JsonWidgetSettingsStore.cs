using CL.WebLogic.Widgets.Models;
using CL.WebLogic.Widgets.Services;

namespace StarterWebsite.Infrastructure;

/// <summary>Widget instance settings in <c>widget-settings.json</c>.</summary>
public sealed class JsonWidgetSettingsStore : IWebWidgetSettingsStore, IDisposable
{
    private readonly JsonFileStore<WebWidgetSettings> _settings;

    /// <summary>Stores in <paramref name="folder"/>.</summary>
    public JsonWidgetSettingsStore(StarterDataFolder folder)
    {
        ArgumentNullException.ThrowIfNull(folder);
        _settings = new JsonFileStore<WebWidgetSettings>(folder.File("widget-settings.json"));
    }

    /// <inheritdoc/>
    public async ValueTask<WebWidgetSettings?> GetAsync(string ownerKey, string instanceId, CancellationToken cancellationToken) =>
        await _settings.GetAsync(Key(ownerKey, instanceId), cancellationToken).ConfigureAwait(false);

    /// <inheritdoc/>
    public async ValueTask SaveAsync(WebWidgetSettings settings, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);
        await _settings.SetAsync(Key(settings.OwnerKey, settings.InstanceId), settings, cancellationToken).ConfigureAwait(false);
    }

    private static string Key(string ownerKey, string instanceId) => ownerKey + "/" + instanceId;

    /// <inheritdoc/>
    public void Dispose() => _settings.Dispose();
}
