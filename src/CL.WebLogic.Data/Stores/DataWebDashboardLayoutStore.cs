using CL.Data.Query;
using CL.WebLogic.Dashboard.Models;
using CL.WebLogic.Dashboard.Services;
using CL.WebLogic.Data.Entities;
using CL.WebLogic.Data.Services;
using CodeLogic.Core.Results;

namespace CL.WebLogic.Data.Stores;

/// <summary>
/// Dashboard layouts in <c>wl_dashboards</c> with optimistic concurrency: a save only succeeds when
/// the stored version is still the one the editor loaded (a conditional <c>UPDATE</c>, so two
/// nodes cannot both win).
/// </summary>
public sealed class DataWebDashboardLayoutStore : IWebDashboardLayoutStore
{
    private readonly WebDataDatabase _db;
    private readonly TimeProvider _time;

    /// <summary>Creates the store.</summary>
    public DataWebDashboardLayoutStore(WebDataDatabase db, TimeProvider? time = null)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _time = time ?? TimeProvider.System;
    }

    /// <inheritdoc/>
    public async ValueTask<WebDashboardLayout?> GetAsync(string ownerKey, string dashboardKey, CancellationToken cancellationToken)
    {
        var key = Key(ownerKey, dashboardKey);
        var row = await _db.FindAsync<DashboardRow>(d => d.LayoutKey == key, cancellationToken).ConfigureAwait(false);
        return row is null ? null : WebDataJson.Read<WebDashboardLayout>(row.RecordJson) with { Version = row.Version };
    }

    /// <inheritdoc/>
    public async ValueTask<Result<WebDashboardLayout>> SaveAsync(WebDashboardLayout layout, long expectedVersion, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(layout);
        var saved = layout with { Version = expectedVersion + 1, UpdatedAt = _time.GetUtcNow() };
        var key = Key(layout.OwnerKey, layout.DashboardKey);
        var json = WebDataJson.Write(saved);

        bool won;
        if (expectedVersion == 0)
        {
            var outcome = WebDataDatabase.Value(await _db.Repository<DashboardRow>().InsertIgnoreAsync(
                new DashboardRow { LayoutKey = key, OwnerKey = layout.OwnerKey, Version = 1, RecordJson = json }, cancellationToken).ConfigureAwait(false));
            won = outcome == InsertOutcome.Inserted;
        }
        else
        {
            var next = expectedVersion + 1;
            won = WebDataDatabase.Value(await _db.Query<DashboardRow>()
                .Where(d => d.LayoutKey == key && d.Version == expectedVersion)
                .UpdateAsync(d => new DashboardRow { Version = next, RecordJson = json }, cancellationToken).ConfigureAwait(false)) == 1;
        }
        return won ? Result<WebDashboardLayout>.Success(saved) : Result<WebDashboardLayout>.Failure(WebDashboardErrors.Conflict());
    }

    private static string Key(string ownerKey, string dashboardKey) => ownerKey + "/" + dashboardKey;
}
