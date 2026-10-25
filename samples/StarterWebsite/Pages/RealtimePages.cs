using CL.WebLogic.Realtime.Services;
using CL.WebLogic.Results;
using CL.WebLogic.Routing;
using CL.WebLogic.Routing.Services;
using CL.WebLogic.Runtime.Models;
using StarterWebsite.Widgets;

namespace StarterWebsite.Pages;

/// <summary>
/// The live wall: signed-in members post short messages, everyone sees them arrive over SSE (or
/// WebSockets). Text is shown with textContent on the client and encoded by templates on the server.
/// </summary>
public sealed class RealtimePages : IWebContributor
{
    private readonly ShoutWall _wall;
    private readonly WebRealtimeHub _hub;

    /// <summary>Creates the contributor.</summary>
    public RealtimePages(ShoutWall wall, WebRealtimeHub hub)
    {
        _wall = wall;
        _hub = hub;
    }

    /// <inheritdoc/>
    public Task ContributeAsync(WebContributionBuilder builder, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.MapPage("/live", (r, _) => ValueTask.FromResult<WebResult>(WebResult.Page("templates/live", new
        {
            Shouts = _wall.Recent(),
            Online = _hub.ConnectionCount,
            _wall.MaxLength
        }, PageModels.Meta("Live wall")))).AllowAnonymous();
        builder.MapPage("/live", PostAsync, "POST").WithRateLimit("forms");
        builder.MapPage("/live/clear", async (r, ct) =>
        {
            await _wall.ClearAsync(ct).ConfigureAwait(false);
            return WebResult.Redirect("/live");
        }, "POST").RequirePermissions("starter.wall.moderate");
        return Task.CompletedTask;
    }

    private async ValueTask<WebResult> PostAsync(WebRequestContext request, CancellationToken cancellationToken)
    {
        var form = await request.ReadFormAsync(cancellationToken).ConfigureAwait(false);
        var result = await _wall.PostAsync(request.Identity.UserName ?? request.Identity.UserId ?? "someone", form["text"].ToString(), cancellationToken).ConfigureAwait(false);
        if (request.IsWebLogicClient)
            return result.IsSuccess ? WebResult.Status(204) : WebResult.Problem(400, result.Error!.Message);
        return WebResult.Redirect("/live");
    }
}
