using CL.WebLogic.Dashboard.Models;
using CL.WebLogic.Dashboard.Services;
using CodeLogic.Core.Results;

namespace MiniBlog.Data.Stores;

/// <summary>Dashboard layouts as versioned documents (the document version is the layout version).</summary>
public sealed class MySqlDashboardLayoutStore : IWebDashboardLayoutStore
{
    private const string Collection = "dashboard";
    private readonly DocumentStore _documents;

    /// <summary>Creates the store.</summary>
    public MySqlDashboardLayoutStore(DocumentStore documents) => _documents = documents;

    /// <inheritdoc/>
    public async ValueTask<WebDashboardLayout?> GetAsync(string ownerKey, string dashboardKey, CancellationToken cancellationToken)
    {
        var (layout, version) = await _documents.GetAsync<WebDashboardLayout>(Collection, ownerKey + "/" + dashboardKey, cancellationToken).ConfigureAwait(false);
        return layout is null ? null : layout with { Version = version };
    }

    /// <inheritdoc/>
    public async ValueTask<Result<WebDashboardLayout>> SaveAsync(WebDashboardLayout layout, long expectedVersion, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(layout);
        var saved = layout with { Version = expectedVersion + 1, UpdatedAt = DateTimeOffset.UtcNow };
        var version = await _documents.TrySetAsync(Collection, layout.OwnerKey + "/" + layout.DashboardKey, saved, expectedVersion, cancellationToken).ConfigureAwait(false);
        return version is null
            ? Result<WebDashboardLayout>.Failure(WebDashboardErrors.Conflict())
            : Result<WebDashboardLayout>.Success(saved);
    }
}
