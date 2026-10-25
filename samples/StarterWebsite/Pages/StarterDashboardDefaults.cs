using CL.WebLogic.Dashboard.Models;
using CL.WebLogic.Dashboard.Services;

namespace StarterWebsite.Pages;

/// <summary>What a new member's dashboard starts with.</summary>
public sealed class StarterDashboardDefaults : IWebDashboardDefaults
{
    /// <inheritdoc/>
    public ValueTask<IReadOnlyList<WebDashboardItem>> GetAsync(string dashboardKey, CancellationToken cancellationToken) =>
        ValueTask.FromResult<IReadOnlyList<WebDashboardItem>>(dashboardKey != DashboardPages.Key ? [] :
        [
            new() { WidgetName = "notes", Zone = "main", Order = 0 },
            new() { WidgetName = "members", Zone = "main", Order = 1 },
            new() { WidgetName = "clock", Zone = "side", Order = 0 },
            new() { WidgetName = "quick-links", Zone = "side", Order = 1 }
        ]);
}
