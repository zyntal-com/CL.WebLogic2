using CL.WebLogic.Widgets.Models;
using CL.WebLogic.Widgets.Services;

namespace MiniBlog.Data.Stores;

/// <summary>Widget instance settings as documents.</summary>
public sealed class MySqlWidgetSettingsStore : IWebWidgetSettingsStore
{
    private const string Collection = "widget";
    private readonly DocumentStore _documents;

    /// <summary>Creates the store.</summary>
    public MySqlWidgetSettingsStore(DocumentStore documents) => _documents = documents;

    /// <inheritdoc/>
    public async ValueTask<WebWidgetSettings?> GetAsync(string ownerKey, string instanceId, CancellationToken cancellationToken) =>
        (await _documents.GetAsync<WebWidgetSettings>(Collection, Key(ownerKey, instanceId), cancellationToken).ConfigureAwait(false)).Value;

    /// <inheritdoc/>
    public async ValueTask SaveAsync(WebWidgetSettings settings, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);
        await _documents.SetAsync(Collection, Key(settings.OwnerKey, settings.InstanceId), settings, cancellationToken).ConfigureAwait(false);
    }

    private static string Key(string ownerKey, string instanceId) => ownerKey + "/" + instanceId;
}
