using CL.WebLogic.Dashboard.Models;

namespace CL.WebLogic.Dashboard.Services;

/// <summary>Dashboards start empty.</summary>
public sealed class EmptyDashboardDefaults : IWebDashboardDefaults
{
    /// <inheritdoc/>
    public ValueTask<IReadOnlyList<WebDashboardItem>> GetAsync(string dashboardKey, CancellationToken cancellationToken) =>
        ValueTask.FromResult<IReadOnlyList<WebDashboardItem>>([]);
}
