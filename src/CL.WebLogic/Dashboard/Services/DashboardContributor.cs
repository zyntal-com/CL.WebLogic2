using System.Text.Json;
using CL.WebLogic.Dashboard.Models;
using CL.WebLogic.Results;
using CL.WebLogic.Routing;
using CL.WebLogic.Routing.Models;
using CL.WebLogic.Routing.Services;
using CL.WebLogic.Runtime.Models;
using CodeLogic.Core.Results;
using Microsoft.AspNetCore.Http;

namespace CL.WebLogic.Dashboard.Services;

/// <summary>
/// Dashboard endpoints, always registered: <c>GET /_weblogic/dashboard/{key}</c> (JSON, or the
/// rendered dashboard with <c>?format=html</c>) and <c>PUT /_weblogic/dashboard/{key}</c>
/// (<c>{ version, items }</c>, CSRF-protected). The owner is resolved on the server.
/// </summary>
public sealed class DashboardContributor : IWebContributor
{
    /// <summary>Contributor id.</summary>
    public const string ContributorId = "weblogic.dashboard";

    /// <summary>URL prefix.</summary>
    public const string Prefix = "/_weblogic/dashboard/";

    private readonly WebDashboardService _dashboards;

    /// <summary>Creates the contributor.</summary>
    public DashboardContributor(WebDashboardService dashboards) => _dashboards = dashboards ?? throw new ArgumentNullException(nameof(dashboards));

    /// <summary>Descriptor.</summary>
    public static WebContributorDescriptor Descriptor { get; } = new()
    {
        Id = ContributorId,
        Name = "WebLogic dashboards",
        Kind = "System",
        Description = "Dashboard layout endpoints (persistence through the app's IWebDashboardLayoutStore)."
    };

    /// <inheritdoc/>
    public Task ContributeAsync(WebContributionBuilder builder, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(builder);
        // Anonymous at the route: the owner resolver decides (the default requires a signed-in user).
        builder.MapApi(Prefix + "{key}", GetAsync, "GET")
            .AllowAnonymous()
            .WithName("weblogic.dashboard.get")
            .WithDescription("The caller's dashboard layout");
        builder.MapApi(Prefix + "{key}", SaveAsync, "PUT", "POST")
            .AllowAnonymous()
            .WithBodyLimit(256 * 1024)
            .WithName("weblogic.dashboard.save")
            .WithDescription("Saves the caller's dashboard layout");
        return Task.CompletedTask;
    }

    private async ValueTask<WebResult> GetAsync(WebRequestContext request, CancellationToken cancellationToken)
    {
        var key = request.GetRouteValue("key") ?? "";
        if (request.GetQuery("format") == "html")
        {
            var zones = request.GetQueryValues("zone");
            var probe = await _dashboards.GetAsync(request.HttpContext, key, cancellationToken).ConfigureAwait(false);
            if (probe.IsFailure)
                return Problem(probe.Error!);
            return WebResult.Html(await _dashboards.RenderAsync(request.HttpContext, key, zones, cancellationToken).ConfigureAwait(false)).WithHeader("X-WebLogic-Fragment", "1");
        }

        var result = await _dashboards.GetAsync(request.HttpContext, key, cancellationToken).ConfigureAwait(false);
        if (result.IsFailure)
            return Problem(result.Error!);
        var available = await _dashboards.AvailableWidgetsAsync(request.HttpContext, cancellationToken).ConfigureAwait(false);
        return WebResult.Json(new
        {
            key = result.Value!.DashboardKey,
            version = result.Value.Version,
            items = result.Value.Items,
            widgets = available.Select(w => new { name = w.Name, description = w.Description, tags = w.Tags })
        });
    }

    private async ValueTask<WebResult> SaveAsync(WebRequestContext request, CancellationToken cancellationToken)
    {
        SaveRequest? body;
        try
        {
            body = await request.ReadJsonAsync<SaveRequest>(cancellationToken).ConfigureAwait(false);
        }
        catch (JsonException)
        {
            return WebResult.Problem(StatusCodes.Status400BadRequest, "The layout is not valid JSON");
        }
        if (body?.Items is null)
            return WebResult.Problem(StatusCodes.Status400BadRequest, "items is required");

        var result = await _dashboards.SaveAsync(request.HttpContext, request.GetRouteValue("key") ?? "", body.Items, body.Version, cancellationToken).ConfigureAwait(false);
        return result.IsSuccess
            ? WebResult.Json(new { key = result.Value!.DashboardKey, version = result.Value.Version, items = result.Value.Items })
            : Problem(result.Error!);
    }

    private static ProblemResult Problem(Error error) => WebResult.Problem(error.Code switch
    {
        WebDashboardErrors.NoOwnerCode => StatusCodes.Status401Unauthorized,
        WebDashboardErrors.ConflictCode => StatusCodes.Status409Conflict,
        WebDashboardErrors.FullCode => StatusCodes.Status503ServiceUnavailable,
        _ => StatusCodes.Status400BadRequest
    }, error.Message);

    private sealed class SaveRequest
    {
        public long Version { get; set; }

        public List<WebDashboardItem>? Items { get; set; }
    }
}
