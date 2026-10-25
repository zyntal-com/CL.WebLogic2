using CL.WebLogic.Configuration.Models;
using CL.WebLogic.Hosting.Services;
using CL.WebLogic.IntegrationTests.Security;
using CL.WebLogic.Results;
using CL.WebLogic.Routing.Models;
using CL.WebLogic.Routing.Services;
using CL.WebLogic.Testing.Services;
using CL.WebLogic.Widgets.Models;
using CL.WebLogic.Widgets.Services;

namespace CL.WebLogic.IntegrationTests.Widgets;

/// <summary>A host with widgets, a widget area and themed pages.</summary>
internal static class WidgetTestSite
{
    public static async Task<WebLogicTestHost> StartAsync(Action<WebLogicConfig>? configure = null, Action<WebLogicBuilder>? web = null, WebLogicTestHostOptions? options = null)
    {
        var host = await SecurityTestSite.StartAsync(configure, builder =>
        {
            builder.AddContributor(new DelegateContributor(Contribute), new WebContributorDescriptor { Id = "tests.widgets", Name = "Widgets" });
            web?.Invoke(builder);
        }, options);
        host.WriteContentFile("theme/layouts/main.html", "<!doctype html><html><head>{renderhead}</head><body>{renderbody}</body></html>");
        host.WriteContentFile("theme/templates/page.html", "{layout:layouts/main}<main>{widget:greeting name=\"<b>Ada</b>\"}{widget:secret}{widget:broken}</main><aside>{widgetarea:side}</aside>");
        host.WriteContentFile("theme/widgets/greeting.html", "<p class=\"greeting\">Hello {model:name}</p>");
        return host;
    }

    private static void Contribute(WebContributionBuilder b)
    {
        b.MapPage("/page", (_, _) => ValueTask.FromResult<WebResult>(WebResult.Page("templates/page"))).AllowAnonymous();
        b.MapPage("/admin/page", (_, _) => ValueTask.FromResult<WebResult>(WebResult.Page("templates/page"))).AllowAnonymous();

        b.MapWidget("greeting", (context, _) => ValueTask.FromResult(WebWidgetResult.Template("widgets/greeting", new { name = context.GetParameter("name", "guest") })),
            o =>
            {
                o.AllowAnonymous();
                o.Description = "Says hello";
            });
        b.MapWidget("secret", (_, _) => ValueTask.FromResult(WebWidgetResult.FromHtml("<p>top secret</p>")));
        b.MapWidget("broken", (_, _) => throw new InvalidOperationException("boom"), o => o.AllowAnonymous());
        b.MapWidget("counter", async (context, ct) =>
        {
            var settings = await context.GetSettingsAsync(ct);
            return WebWidgetResult.FromHtml("<span class=\"count\">" + settings.GetValueOrDefault("count", "0") + "</span>");
        }, o =>
        {
            o.AllowAnonymous();
            o.Dashboard = false;
            o.Action("increment", async (action, ct) =>
            {
                var current = int.Parse((await action.Widget.GetSettingsAsync(ct)).GetValueOrDefault("count", "0"), System.Globalization.CultureInfo.InvariantCulture);
                var step = int.TryParse(action.GetArgument("by"), out var by) ? by : 1;
                await action.SaveSettingsAsync(new Dictionary<string, string> { ["count"] = (current + step).ToString(System.Globalization.CultureInfo.InvariantCulture) }, ct);
                return await action.RefreshAsync(ct);
            });
        });

        b.PlaceWidget("side", "greeting", o =>
        {
            o.Order = 2;
            o.Parameters["name"] = "sidebar";
        });
        b.PlaceWidget("side", "counter", o =>
        {
            o.Order = 1;
            o.InstanceId = "side-counter";
            o.ExcludePaths.Add("/admin/**");
        });
    }
}
