using CL.WebLogic.Dashboard.Models;
using CodeLogic.Core.Results;

namespace CL.WebLogic.Dashboard.Services;

/// <summary>Development default: layouts in memory (lost on restart; a warning is logged outside Development).</summary>
public sealed class InMemoryDashboardLayoutStore : IWebDashboardLayoutStore, Security.Services.IWebDevelopmentStore
{
    private const int MaxLayouts = 10000;
    private readonly Dictionary<(string, string), WebDashboardLayout> _layouts = [];
    private readonly Lock _lock = new();
    private readonly TimeProvider _time;

    /// <summary>Creates the store.</summary>
    public InMemoryDashboardLayoutStore(TimeProvider? time = null) => _time = time ?? TimeProvider.System;

    /// <inheritdoc/>
    public ValueTask<WebDashboardLayout?> GetAsync(string ownerKey, string dashboardKey, CancellationToken cancellationToken)
    {
        lock (_lock)
            return ValueTask.FromResult(_layouts.GetValueOrDefault((ownerKey, dashboardKey)));
    }

    /// <inheritdoc/>
    public ValueTask<Result<WebDashboardLayout>> SaveAsync(WebDashboardLayout layout, long expectedVersion, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(layout);
        lock (_lock)
        {
            var key = (layout.OwnerKey, layout.DashboardKey);
            var current = _layouts.GetValueOrDefault(key)?.Version ?? 0;
            if (current != expectedVersion)
                return ValueTask.FromResult(Result<WebDashboardLayout>.Failure(WebDashboardErrors.Conflict()));
            if (current == 0 && _layouts.Count >= MaxLayouts)
                return ValueTask.FromResult(Result<WebDashboardLayout>.Failure(WebDashboardErrors.Full("The in-memory dashboard store is full.")));
            var saved = layout with { Version = current + 1, UpdatedAt = _time.GetUtcNow() };
            _layouts[key] = saved;
            return ValueTask.FromResult(Result<WebDashboardLayout>.Success(saved));
        }
    }
}
