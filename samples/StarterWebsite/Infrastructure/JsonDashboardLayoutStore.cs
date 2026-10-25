using CL.WebLogic.Dashboard.Models;
using CL.WebLogic.Dashboard.Services;
using CodeLogic.Core.Results;

namespace StarterWebsite.Infrastructure;

/// <summary>
/// Dashboard layouts in <c>dashboards.json</c>. The toolkit decides who the owner is (the signed-in
/// user, hashed); this store only keeps the layout and enforces the optimistic version.
/// </summary>
public sealed class JsonDashboardLayoutStore : IWebDashboardLayoutStore, IDisposable
{
    private readonly JsonFileStore<WebDashboardLayout> _layouts;
    private readonly TimeProvider _time;

    /// <summary>Stores in <paramref name="folder"/>.</summary>
    public JsonDashboardLayoutStore(StarterDataFolder folder, TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(folder);
        _layouts = new JsonFileStore<WebDashboardLayout>(folder.File("dashboards.json"));
        _time = time;
    }

    /// <inheritdoc/>
    public async ValueTask<WebDashboardLayout?> GetAsync(string ownerKey, string dashboardKey, CancellationToken cancellationToken) =>
        await _layouts.GetAsync(Key(ownerKey, dashboardKey), cancellationToken).ConfigureAwait(false);

    /// <inheritdoc/>
    public async ValueTask<Result<WebDashboardLayout>> SaveAsync(WebDashboardLayout layout, long expectedVersion, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(layout);
        return await _layouts.UpdateAsync(items =>
        {
            var key = Key(layout.OwnerKey, layout.DashboardKey);
            var current = items.GetValueOrDefault(key)?.Version ?? 0;
            if (current != expectedVersion)
                return (false, Result<WebDashboardLayout>.Failure(WebDashboardErrors.Conflict()));
            var saved = layout with { Version = current + 1, UpdatedAt = _time.GetUtcNow() };
            items[key] = saved;
            return (true, Result<WebDashboardLayout>.Success(saved));
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public void Dispose() => _layouts.Dispose();

    private static string Key(string ownerKey, string dashboardKey) => ownerKey + "/" + dashboardKey;
}
