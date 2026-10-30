using CL.WebLogic.Hosting.Models;
using CL.WebLogic.Routing;
using CL.WebLogic.Routing.Models;
using CL.WebLogic.Routing.Services;
using CL.WebLogic.Security.Services;
using CL.WebLogic.Theming.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace CL.WebLogic.Hosting.Services;

/// <summary>
/// Registers the toolkit's system contributors and every application contributor added with
/// <c>web.AddContributor</c> when the host starts. A failing application contributor stops
/// startup — a half-registered site is worse than a clear error.
/// </summary>
public sealed class ContributorBootstrapper : IHostedService
{
    private readonly ContributorRegistrar _registrar;
    private readonly IServiceProvider _services;
    private readonly IEnumerable<WebContributorRegistration> _registrations;

    /// <summary>Creates the bootstrapper.</summary>
    public ContributorBootstrapper(
        ContributorRegistrar registrar,
        IServiceProvider services,
        IEnumerable<WebContributorRegistration> registrations)
    {
        _registrar = registrar ?? throw new ArgumentNullException(nameof(registrar));
        _services = services ?? throw new ArgumentNullException(nameof(services));
        _registrations = registrations ?? [];
    }

    /// <inheritdoc/>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await _registrar.RegisterAsync(
            _services.GetRequiredService<ThemeAssetContributor>(),
            ThemeAssetContributor.Descriptor,
            cancellationToken).ConfigureAwait(false);

        await _registrar.RegisterAsync(
            _services.GetRequiredService<PluginAssetContributor>(),
            PluginAssetContributor.Descriptor,
            cancellationToken).ConfigureAwait(false);

        await _registrar.RegisterAsync(
            _services.GetRequiredService<SecurityContributor>(),
            SecurityContributor.Descriptor,
            cancellationToken).ConfigureAwait(false);

        await _registrar.RegisterAsync(
            _services.GetRequiredService<Client.Services.ClientContributor>(),
            Client.Services.ClientContributor.Descriptor,
            cancellationToken).ConfigureAwait(false);

        await _registrar.RegisterAsync(
            _services.GetRequiredService<Forms.Services.FormsContributor>(),
            Forms.Services.FormsContributor.Descriptor,
            cancellationToken).ConfigureAwait(false);

        await _registrar.RegisterAsync(
            _services.GetRequiredService<Widgets.Services.WidgetsContributor>(),
            Widgets.Services.WidgetsContributor.Descriptor,
            cancellationToken).ConfigureAwait(false);

        await _registrar.RegisterAsync(
            _services.GetRequiredService<Dashboard.Services.DashboardContributor>(),
            Dashboard.Services.DashboardContributor.Descriptor,
            cancellationToken).ConfigureAwait(false);

        await _registrar.RegisterAsync(
            _services.GetRequiredService<Realtime.Services.RealtimeContributor>(),
            Realtime.Services.RealtimeContributor.Descriptor,
            cancellationToken).ConfigureAwait(false);

        await _registrar.RegisterAsync(
            _services.GetRequiredService<Localization.Services.LocalizationContributor>(),
            Localization.Services.LocalizationContributor.Descriptor,
            cancellationToken).ConfigureAwait(false);

        await _registrar.RegisterAsync(
            _services.GetRequiredService<Explorer.Services.ExplorerContributor>(),
            Explorer.Services.ExplorerContributor.Descriptor,
            cancellationToken).ConfigureAwait(false);

        foreach (var registration in _registrations)
        {
            var contributor = registration.Instance
                ?? (IWebContributor)_services.GetRequiredService(registration.ContributorType);
            var descriptor = registration.Descriptor
                ?? WebContributorDescriptor.FromType(registration.ContributorType, "Application");

            await _registrar.RegisterAsync(contributor, descriptor, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
