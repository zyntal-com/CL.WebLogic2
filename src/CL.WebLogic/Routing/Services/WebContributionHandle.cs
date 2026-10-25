using CL.WebLogic.Routing.Models;

namespace CL.WebLogic.Routing.Services;

/// <summary>
/// Represents one registered contributor. Disposing it removes everything the contributor
/// registered — routes and any additional registrations tracked through
/// <see cref="WebContributionBuilder.OnRemove"/> (widgets, providers, realtime mappings…).
/// </summary>
public sealed class WebContributionHandle : IAsyncDisposable
{
    private readonly ContributorRegistrar _registrar;
    private int _disposed;

    internal WebContributionHandle(ContributorRegistrar registrar, WebContributorDescriptor contributor, int routeCount)
    {
        _registrar = registrar;
        Contributor = contributor;
        RouteCount = routeCount;
    }

    /// <summary>The contributor this handle represents.</summary>
    public WebContributorDescriptor Contributor { get; }

    /// <summary>Number of routes registered by the contributor.</summary>
    public int RouteCount { get; }

    /// <summary>True once the contributor has been removed.</summary>
    public bool IsRemoved => Volatile.Read(ref _disposed) == 1;

    /// <summary>Removes the contributor's registrations. Safe to call more than once.</summary>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1) return;
        await _registrar.UnregisterAsync(this, CancellationToken.None).ConfigureAwait(false);
    }
}
