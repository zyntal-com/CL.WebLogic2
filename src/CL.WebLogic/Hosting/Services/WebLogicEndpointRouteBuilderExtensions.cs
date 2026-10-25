using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace CL.WebLogic.Hosting.Services;

/// <summary>Maps WebLogic's endpoints into the ASP.NET route table.</summary>
public static class WebLogicEndpointRouteBuilderExtensions
{
    /// <summary>Liveness probe path (always 200 while the process serves requests).</summary>
    public const string LivenessPath = "/_weblogic/health/live";

    /// <summary>Readiness probe path (runs every check tagged <c>ready</c>).</summary>
    public const string ReadinessPath = "/_weblogic/health/ready";

    /// <summary>
    /// Maps every contributed WebLogic route (pages, APIs, assets, fallback) and the health
    /// probes. Contributed routes stay live: adding or removing a contributor updates the table.
    /// </summary>
    public static IEndpointRouteBuilder MapCodeLogicWebLogic(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.DataSources.Add(endpoints.ServiceProvider.GetRequiredService<WebLogicEndpointDataSource>());
        endpoints.MapHealthChecks(LivenessPath, new HealthCheckOptions { Predicate = _ => false });
        endpoints.MapHealthChecks(ReadinessPath, new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });
        return endpoints;
    }
}
