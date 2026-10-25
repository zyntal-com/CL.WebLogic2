using CL.WebLogic.Dashboard.Models;

namespace CL.WebLogic.Dashboard.Services;

/// <summary>The layout a dashboard starts with before its owner saved one.</summary>
public interface IWebDashboardDefaults
{
    /// <summary>Default items (empty for none).</summary>
    ValueTask<IReadOnlyList<WebDashboardItem>> GetAsync(string dashboardKey, CancellationToken cancellationToken);
}
