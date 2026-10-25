using CL.WebLogic.Dashboard.Services;
using CL.WebLogic.Results;
using CL.WebLogic.Routing;
using CL.WebLogic.Routing.Services;
using CL.WebLogic.Templates.Escaping;

namespace StarterWebsite.Pages;

/// <summary>The personal dashboard (signed-in users). Layouts are saved per user by the toolkit's endpoints.</summary>
public sealed class DashboardPages : IWebContributor
{
    /// <summary>The dashboard key used by the page.</summary>
    public const string Key = "home";

    private readonly WebDashboardService _dashboards;

    /// <summary>Creates the contributor.</summary>
    public DashboardPages(WebDashboardService dashboards) => _dashboards = dashboards;

    /// <inheritdoc/>
    public Task ContributeAsync(WebContributionBuilder builder, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.MapPage("/dashboard", async (r, ct) =>
        {
            var html = await _dashboards.RenderAsync(r.HttpContext, Key, ["main", "side"], ct).ConfigureAwait(false);
            var widgets = await _dashboards.AvailableWidgetsAsync(r.HttpContext, ct).ConfigureAwait(false);
            return WebResult.Page("templates/dashboard", new
            {
                Dashboard = new WebHtml(html),
                Key,
                Widgets = widgets.Select(w => new { w.Name, w.Description }).ToList()
            }, PageModels.Meta("Dashboard"));
        });
        return Task.CompletedTask;
    }
}
