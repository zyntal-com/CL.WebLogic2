using Microsoft.AspNetCore.Http;

namespace CL.WebLogic.Dashboard.Services;

/// <summary>
/// Decides whose dashboard a request reads and writes — always on the server. Null means the
/// caller has no dashboard (401). The default gives each signed-in user their own.
/// </summary>
public interface IWebDashboardOwnerResolver
{
    /// <summary>The owner key (<c>^[A-Za-z0-9_.:-]{1,128}$</c>), or null.</summary>
    ValueTask<string?> ResolveAsync(HttpContext httpContext, string dashboardKey, CancellationToken cancellationToken);
}
