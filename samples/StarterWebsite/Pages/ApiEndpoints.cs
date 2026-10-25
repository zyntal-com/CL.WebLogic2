using CL.WebLogic.Auth.Users;
using CL.WebLogic.Results;
using CL.WebLogic.Routing;
using CL.WebLogic.Routing.Services;
using StarterWebsite.Application;
using StarterWebsite.Widgets;

namespace StarterWebsite.Pages;

/// <summary>
/// <c>/api/v1</c> for API keys (<c>X-Api-Key: wl_…</c>). Keys carry scopes; the policy demands
/// <c>profile:read</c>, posting to the wall additionally <c>wall:write</c>. Key requests are not
/// ambient, so no CSRF token is needed.
/// </summary>
public sealed class ApiEndpoints : IWebContributor
{
    private readonly ShoutWall _wall;
    private readonly IWebUserStore _users;

    /// <summary>Creates the contributor.</summary>
    public ApiEndpoints(ShoutWall wall, IWebUserStore users)
    {
        _wall = wall;
        _users = users;
    }

    /// <inheritdoc/>
    public Task ContributeAsync(WebContributionBuilder builder, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(builder);
        var api = builder.MapGroup("/api/v1").RequirePolicy(StarterSetup.ApiPolicy).WithRateLimit("api");
        // For a key, Identity.UserName is the key's name; the owner is Identity.UserId.
        api.MapApi("/me", async (r, ct) => WebResult.Json(new
        {
            id = r.Identity.UserId,
            name = (await OwnerAsync(r, ct).ConfigureAwait(false))?.UserName,
            key = r.Identity.UserName,
            groups = r.Identity.Groups,
            scopes = r.Identity.Scopes,
            scheme = r.Identity.Scheme
        })).WithDescription("The key's owner.");
        api.MapApi("/wall", async (r, ct) =>
        {
            if (!r.Identity.Scopes.Contains("wall:write"))
                return WebResult.Problem(403, "Missing scope", "This key needs the wall:write scope.");
            var body = await r.ReadJsonAsync<WallPost>(ct).ConfigureAwait(false);
            var author = (await OwnerAsync(r, ct).ConfigureAwait(false))?.UserName ?? "api";
            var result = await _wall.PostAsync(author, body?.Text ?? string.Empty, ct).ConfigureAwait(false);
            return result.IsSuccess ? WebResult.Status(202) : WebResult.Problem(400, result.Error!.Message);
        }, "POST").WithDescription("Post to the live wall (scope wall:write).");
        return Task.CompletedTask;
    }

    private Task<WebUser?> OwnerAsync(CL.WebLogic.Runtime.Models.WebRequestContext request, CancellationToken cancellationToken) =>
        request.Identity.UserId is { } id ? _users.FindByIdAsync(id, cancellationToken) : Task.FromResult<WebUser?>(null);

    private sealed record WallPost(string? Text);
}
