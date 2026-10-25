using CL.WebLogic.Realtime.Services;
using CL.WebLogic.Results;
using CL.WebLogic.Routing;
using CL.WebLogic.Routing.Services;
using StarterWebsite.Configuration;
using StarterWebsite.Infrastructure;

namespace StarterWebsite.Pages;

/// <summary>The landing page.</summary>
public sealed class HomePages : IWebContributor
{
    private readonly JsonUserStore _users;
    private readonly WebRealtimeHub _hub;
    private readonly StarterConfig _config;

    /// <summary>Creates the contributor.</summary>
    public HomePages(CL.WebLogic.Auth.Users.IWebUserStore users, WebRealtimeHub hub, StarterConfig config)
    {
        _users = (JsonUserStore)users;
        _hub = hub;
        _config = config;
    }

    /// <inheritdoc/>
    public Task ContributeAsync(WebContributionBuilder builder, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.MapPage("/", async (r, ct) => WebResult.Page("templates/home", new
        {
            Members = await _users.CountAsync(ct).ConfigureAwait(false),
            Online = _hub.ConnectionCount,
            Registration = _config.AllowRegistration
        }, PageModels.Meta("Home", "The reference website for CL.WebLogic"))).AllowAnonymous().WithName("home");
        return Task.CompletedTask;
    }
}
