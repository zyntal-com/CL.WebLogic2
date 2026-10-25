using System.Collections.Concurrent;
using CL.WebLogic.Hosting.Services;
using CL.WebLogic.Routing.Events;
using CL.WebLogic.Routing.Models;

namespace CL.WebLogic.Routing.Services;

/// <summary>
/// Registers and removes <see cref="IWebContributor"/>s. A contribution is all-or-nothing:
/// routes are staged on a <see cref="WebContributionBuilder"/> and committed only if the
/// contributor completes without throwing.
/// </summary>
public sealed class ContributorRegistrar : IDisposable
{
    private readonly WebRouteRegistry _routes;
    private readonly IServiceProvider _services;
    private readonly IWebLogicContext _context;
    private readonly ConcurrentDictionary<string, Registration> _registrations = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <summary>Creates the registrar.</summary>
    public ContributorRegistrar(WebRouteRegistry routes, IServiceProvider services, IWebLogicContext context)
    {
        _routes = routes ?? throw new ArgumentNullException(nameof(routes));
        _services = services ?? throw new ArgumentNullException(nameof(services));
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <summary>Descriptors of every registered contributor.</summary>
    public IReadOnlyList<WebContributorDescriptor> Contributors =>
        _registrations.Values.Select(r => r.Handle.Contributor).ToList();

    /// <summary>True when a contributor with <paramref name="contributorId"/> is registered.</summary>
    public bool IsRegistered(string contributorId) => _registrations.ContainsKey(contributorId);

    /// <summary>Runs <paramref name="contributor"/> and commits its registrations.</summary>
    public async Task<WebContributionHandle> RegisterAsync(
        IWebContributor contributor,
        WebContributorDescriptor? descriptor,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(contributor);
        descriptor ??= WebContributorDescriptor.FromType(contributor.GetType());

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_registrations.ContainsKey(descriptor.Id))
                throw new InvalidOperationException($"Contributor '{descriptor.Id}' is already registered.");

            var builder = new WebContributionBuilder(descriptor, _services);
            await contributor.ContributeAsync(builder, cancellationToken).ConfigureAwait(false);

            var routes = builder.Build();
            _routes.Add(descriptor.Id, routes);
            try
            {
                foreach (var commit in builder.Commits)
                    commit();
            }
            catch
            {
                // All or nothing: undo the routes and whatever the commits already registered.
                _routes.Remove(descriptor.Id);
                await RunCleanupsAsync(descriptor.Id, builder.Cleanups).ConfigureAwait(false);
                throw;
            }

            var handle = new WebContributionHandle(this, descriptor, routes.Count);
            _registrations[descriptor.Id] = new Registration(handle, builder.Cleanups);

            LogRegistration(descriptor, routes);
            await _context.Events.PublishAsync(
                new WebContributorRegisteredEvent(descriptor.Id, descriptor.Name, descriptor.Kind, routes.Count)).ConfigureAwait(false);
            return handle;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Removes a contributor and everything it registered.</summary>
    /// <returns>True when the contributor was registered.</returns>
    public Task<bool> UnregisterAsync(string contributorId, CancellationToken cancellationToken) =>
        UnregisterCoreAsync(contributorId, expected: null, cancellationToken);

    /// <summary>Removes the registration represented by <paramref name="handle"/> — never a newer one that reused the id.</summary>
    internal Task<bool> UnregisterAsync(WebContributionHandle handle, CancellationToken cancellationToken) =>
        UnregisterCoreAsync(handle.Contributor.Id, handle, cancellationToken);

    private async Task<bool> UnregisterCoreAsync(string contributorId, WebContributionHandle? expected, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!_registrations.TryGetValue(contributorId, out var registration)
                || (expected is not null && !ReferenceEquals(registration.Handle, expected)))
                return false;

            _registrations.TryRemove(contributorId, out _);

            _routes.Remove(contributorId);
            await RunCleanupsAsync(contributorId, registration.Cleanups).ConfigureAwait(false);

            _context.Logger.Info($"Removed web contributor '{contributorId}'");
            await _context.Events.PublishAsync(new WebContributorRemovedEvent(contributorId)).ConfigureAwait(false);
            return true;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task RunCleanupsAsync(string contributorId, IReadOnlyList<Func<ValueTask>> cleanups)
    {
        foreach (var cleanup in cleanups)
        {
            try
            {
                await cleanup().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _context.Logger.Error($"Cleanup for contributor '{contributorId}' failed: {ex.Message}", ex);
            }
        }
    }

    /// <inheritdoc/>
    public void Dispose() => _gate.Dispose();

    private void LogRegistration(WebContributorDescriptor descriptor, IReadOnlyList<WebRouteDefinition> routes)
    {
        _context.Logger.Info($"Registered web contributor '{descriptor.Id}' ({descriptor.Kind}) with {routes.Count} route(s)");

        // Secure by default: every opt-out is visible at startup.
        foreach (var route in routes.Where(r => r.Metadata.AllowAnonymous))
            _context.Logger.Info($"  anonymous route: {string.Join(",", route.Metadata.Methods)} {route.Metadata.Pattern}");
    }

    private sealed record Registration(WebContributionHandle Handle, IReadOnlyList<Func<ValueTask>> Cleanups);
}
