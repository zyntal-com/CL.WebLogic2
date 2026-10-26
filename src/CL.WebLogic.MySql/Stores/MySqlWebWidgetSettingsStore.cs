using CL.WebLogic.MySql.Entities;
using CL.WebLogic.MySql.Services;
using CL.WebLogic.Widgets.Models;
using CL.WebLogic.Widgets.Services;

namespace CL.WebLogic.MySql.Stores;

/// <summary>Widget instance settings in <c>wl_widget_settings</c>.</summary>
public sealed class MySqlWebWidgetSettingsStore : IWebWidgetSettingsStore
{
    private readonly WebMySqlDatabase _db;

    /// <summary>Creates the store.</summary>
    public MySqlWebWidgetSettingsStore(WebMySqlDatabase db) => _db = db ?? throw new ArgumentNullException(nameof(db));

    /// <inheritdoc/>
    public async ValueTask<WebWidgetSettings?> GetAsync(string ownerKey, string instanceId, CancellationToken cancellationToken)
    {
        var row = WebMySqlDatabase.Value(await _db.Repository<WidgetSettingsRow>().GetByIdAsync(Key(ownerKey, instanceId), cancellationToken).ConfigureAwait(false));
        return row is null ? null : WebMySqlJson.Read<WebWidgetSettings>(row.RecordJson);
    }

    /// <inheritdoc/>
    public async ValueTask SaveAsync(WebWidgetSettings settings, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);
        WebMySqlDatabase.Value(await _db.Repository<WidgetSettingsRow>().UpsertAsync(new WidgetSettingsRow
        {
            SettingsKey = Key(settings.OwnerKey, settings.InstanceId),
            OwnerKey = settings.OwnerKey,
            RecordJson = WebMySqlJson.Write(settings)
        }, cancellationToken).ConfigureAwait(false));
    }

    private static string Key(string ownerKey, string instanceId) => ownerKey + "/" + instanceId;
}
