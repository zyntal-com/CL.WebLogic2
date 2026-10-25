using CL.WebLogic.Dashboard.Models;
using CodeLogic.Core.Results;

namespace CL.WebLogic.Dashboard.Services;

/// <summary>Where dashboard layouts live. The app supplies it (per-user database rows, JSON files…).</summary>
public interface IWebDashboardLayoutStore
{
    /// <summary>The layout, or null when never saved.</summary>
    ValueTask<WebDashboardLayout?> GetAsync(string ownerKey, string dashboardKey, CancellationToken cancellationToken);

    /// <summary>
    /// Saves when the stored version equals <paramref name="expectedVersion"/> (0 = not saved yet);
    /// returns the saved layout with the next version, or <see cref="WebDashboardErrors.Conflict"/>
    /// (any other error code is answered with 400).
    /// </summary>
    ValueTask<Result<WebDashboardLayout>> SaveAsync(WebDashboardLayout layout, long expectedVersion, CancellationToken cancellationToken);
}
