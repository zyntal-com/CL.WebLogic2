using CL.WebLogic.Widgets.Models;

namespace CL.WebLogic.Widgets.Services;

/// <summary>
/// Where widget instance settings live. The app supplies it (file, JSON, database…). Settings are
/// keyed by owner <em>and</em> instance id: the instance id comes from the client and is not a secret.
/// </summary>
public interface IWebWidgetSettingsStore
{
    /// <summary>Settings of an instance for <paramref name="ownerKey"/>, or null.</summary>
    ValueTask<WebWidgetSettings?> GetAsync(string ownerKey, string instanceId, CancellationToken cancellationToken);

    /// <summary>Saves settings for <see cref="WebWidgetSettings.OwnerKey"/> (replacing previous values).</summary>
    ValueTask SaveAsync(WebWidgetSettings settings, CancellationToken cancellationToken);
}
