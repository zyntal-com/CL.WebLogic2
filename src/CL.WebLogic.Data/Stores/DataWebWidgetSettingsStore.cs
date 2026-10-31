using CL.WebLogic.Data.Entities;
using CL.WebLogic.Data.Services;
using CL.WebLogic.Widgets.Models;
using CL.WebLogic.Widgets.Services;

namespace CL.WebLogic.Data.Stores;

/// <summary>Widget instance settings in <c>wl_widget_settings</c>.</summary>
public sealed class DataWebWidgetSettingsStore : IWebWidgetSettingsStore
{
    private readonly WebDataDatabase _db;

    /// <summary>Creates the store.</summary>
    public DataWebWidgetSettingsStore(WebDataDatabase db) => _db = db ?? throw new ArgumentNullException(nameof(db));

    /// <inheritdoc/>
    public async ValueTask<WebWidgetSettings?> GetAsync(string ownerKey, string instanceId, CancellationToken cancellationToken)
    {
        var key = Key(ownerKey, instanceId);
        var row = await _db.FindAsync<WidgetSettingsRow>(w => w.SettingsKey == key, cancellationToken).ConfigureAwait(false);
        return row is null ? null : WebDataJson.Read<WebWidgetSettings>(row.RecordJson);
    }

    /// <inheritdoc/>
    public async ValueTask SaveAsync(WebWidgetSettings settings, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);
        WebDataDatabase.Value(await _db.Repository<WidgetSettingsRow>().UpsertAsync(new WidgetSettingsRow
        {
            SettingsKey = Key(settings.OwnerKey, settings.InstanceId),
            OwnerKey = settings.OwnerKey,
            RecordJson = WebDataJson.Write(settings)
        }, ct: cancellationToken).ConfigureAwait(false));
    }

    private static string Key(string ownerKey, string instanceId) => ownerKey + "/" + instanceId;
}
