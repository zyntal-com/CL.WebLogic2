namespace CL.WebLogic.Widgets.Models;

/// <summary>
/// Saved settings of a widget instance for one owner. The owner comes from the server
/// (<see cref="Dashboard.Services.IWebDashboardOwnerResolver"/>), never from the request, so a
/// guessed or copied instance id only ever reaches the caller's own settings.
/// </summary>
public sealed record WebWidgetSettings(string OwnerKey, string InstanceId, string WidgetName, IReadOnlyDictionary<string, string> Values, DateTimeOffset UpdatedAt);
