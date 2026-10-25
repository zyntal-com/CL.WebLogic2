using CL.WebLogic.Dashboard.Services;
using CL.WebLogic.Realtime.Services;
using CL.WebLogic.Results;
using CL.WebLogic.Routing;
using CL.WebLogic.Routing.Services;
using CL.WebLogic.Widgets.Models;
using CL.WebLogic.Widgets.Services;
using StarterWebsite.Infrastructure;
using StarterWebsite.Pages;

namespace StarterWebsite.Widgets;

/// <summary>
/// The starter's widgets: usable in templates (<c>{widget:clock}</c>), in the home page's
/// <c>home</c> area and on dashboards.
/// </summary>
public sealed class StarterWidgets : IWebContributor
{
    private const int MaxNoteLength = 500;
    private readonly JsonUserStore _users;
    private readonly WebRealtimeHub _hub;
    private readonly TimeProvider _time;

    /// <summary>Creates the contributor.</summary>
    public StarterWidgets(CL.WebLogic.Auth.Users.IWebUserStore users, WebRealtimeHub hub, TimeProvider time)
    {
        _users = (JsonUserStore)users;
        _hub = hub;
        _time = time;
    }

    /// <inheritdoc/>
    public Task ContributeAsync(WebContributionBuilder builder, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.MapWidget("clock", (_, _) => ValueTask.FromResult(WebWidgetResult.Template("widgets/clock", new
        {
            Time = _time.GetUtcNow().ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture),
            Date = _time.GetUtcNow().ToString("dddd d MMMM", System.Globalization.CultureInfo.InvariantCulture)
        })), o =>
        {
            o.AllowAnonymous();
            o.Description = "Server time (UTC), refreshed every 30 seconds";
            o.RefreshSeconds = 30;
        });

        builder.MapWidget("quick-links", (_, _) => ValueTask.FromResult(WebWidgetResult.Template("widgets/quick-links", null)), o =>
        {
            o.AllowAnonymous();
            o.Description = "Links to the demo areas";
        });

        builder.MapWidget("members", async (_, ct) => WebWidgetResult.Template("widgets/members", new
        {
            Members = await _users.CountAsync(ct).ConfigureAwait(false),
            Online = _hub.ConnectionCount
        }), o =>
        {
            o.Description = "Member count and live connections";
            o.RefreshSeconds = 15;
        });

        builder.MapWidget("notes", async (context, ct) =>
        {
            var settings = await context.GetSettingsAsync(ct).ConfigureAwait(false);
            return WebWidgetResult.Template("widgets/notes", new { Text = settings.GetValueOrDefault("text", string.Empty), context.InstanceId, MaxNoteLength });
        }, o =>
        {
            o.Description = "A private note, saved per dashboard item";
            o.Action("save", SaveNoteAsync);
        });

        builder.PlaceWidget("home", "clock", o => o.Order = 1);
        builder.PlaceWidget("home", "quick-links", o => o.Order = 2);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Settings are already scoped to the caller by the toolkit; the action also requires the note
    /// to be on the caller's dashboard, so clients cannot create notes for arbitrary instance ids.
    /// </summary>
    private static async ValueTask<WebResult> SaveNoteAsync(WebWidgetActionContext action, CancellationToken cancellationToken)
    {
        var dashboards = action.Widget.GetRequiredService<WebDashboardService>();
        var layout = await dashboards.GetAsync(action.Widget.HttpContext, DashboardPages.Key, cancellationToken).ConfigureAwait(false);
        if (layout.IsFailure || !layout.Value!.Items.Any(i => i.InstanceId == action.Widget.InstanceId && i.WidgetName == "notes"))
            return WebResult.Problem(403, "Not your note");
        var text = action.GetArgument("text") ?? string.Empty;
        if (text.Length > MaxNoteLength)
            return WebResult.Problem(400, $"Notes are at most {MaxNoteLength} characters.");
        if (!await action.SaveSettingsAsync(new Dictionary<string, string> { ["text"] = text }, cancellationToken).ConfigureAwait(false))
            return WebResult.Problem(401, "Sign in to keep notes");
        return await action.RefreshAsync(cancellationToken).ConfigureAwait(false);
    }
}
