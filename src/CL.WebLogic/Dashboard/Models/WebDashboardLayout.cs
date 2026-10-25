namespace CL.WebLogic.Dashboard.Models;

/// <summary>A saved dashboard: whose it is, which dashboard, and its items.</summary>
public sealed record WebDashboardLayout
{
    /// <summary>Owner (resolved on the server by <c>IWebDashboardOwnerResolver</c>, never taken from the request).</summary>
    public required string OwnerKey { get; init; }

    /// <summary>Dashboard key, e.g. <c>home</c> or <c>admin</c>.</summary>
    public required string DashboardKey { get; init; }

    /// <summary>Items in zone and order.</summary>
    public IReadOnlyList<WebDashboardItem> Items { get; init; } = [];

    /// <summary>Optimistic-concurrency version (incremented on every save; 0 = never saved).</summary>
    public long Version { get; init; }

    /// <summary>Last save.</summary>
    public DateTimeOffset UpdatedAt { get; init; }
}
