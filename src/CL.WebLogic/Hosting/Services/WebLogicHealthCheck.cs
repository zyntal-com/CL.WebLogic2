using CodeLogic.Framework.Libraries;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CL.WebLogic.Hosting.Services;

/// <summary>ASP.NET readiness check backed by the WebLogic library's CodeLogic health.</summary>
public sealed class WebLogicHealthCheck : IHealthCheck
{
    private readonly IWebLogicContext _context;

    /// <summary>Creates the check.</summary>
    public WebLogicHealthCheck(IWebLogicContext context) =>
        _context = context ?? throw new ArgumentNullException(nameof(context));

    /// <inheritdoc/>
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var status = await _context.CheckHealthAsync(cancellationToken).ConfigureAwait(false);
        return status.Status switch
        {
            HealthStatusLevel.Healthy => HealthCheckResult.Healthy(status.Message),
            HealthStatusLevel.Degraded => HealthCheckResult.Degraded(status.Message),
            _ => HealthCheckResult.Unhealthy(status.Message)
        };
    }
}
