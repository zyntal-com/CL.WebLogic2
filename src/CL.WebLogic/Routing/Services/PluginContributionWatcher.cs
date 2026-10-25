using CL.WebLogic.Hosting.Services;
using CL.WebLogic.Routing.Models;
using CodeLogic.Core.Events;
using CodeLogic.Framework.Application.Plugins;
using Microsoft.Extensions.Hosting;

namespace CL.WebLogic.Routing.Services;

/// <summary>
/// Registers loaded CodeLogic plugins that implement <see cref="IWebContributor"/>, and removes
/// their routes and other registrations again when the plugin unloads. Handler failures are
/// logged and never crash the host.
/// </summary>
public sealed class PluginContributionWatcher : IHostedService, IDisposable
{
    private readonly ContributorRegistrar _registrar;
    private readonly IWebPluginSource _plugins;
    private readonly IWebLogicContext _context;
    private readonly List<IEventSubscription> _subscriptions = [];

    /// <summary>Creates the watcher.</summary>
    public PluginContributionWatcher(ContributorRegistrar registrar, IWebPluginSource plugins, IWebLogicContext context)
    {
        _registrar = registrar ?? throw new ArgumentNullException(nameof(registrar));
        _plugins = plugins ?? throw new ArgumentNullException(nameof(plugins));
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <inheritdoc/>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _subscriptions.Add(_context.Events.SubscribeAsync<PluginLoadedEvent>(e => OnLoadedAsync(e.PluginId)));
        _subscriptions.Add(_context.Events.SubscribeAsync<PluginUnloadedEvent>(e => OnUnloadedAsync(e.PluginId)));

        foreach (var plugin in _plugins.GetLoadedPlugins())
            await TryRegisterAsync(plugin, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public Task StopAsync(CancellationToken cancellationToken)
    {
        DisposeSubscriptions();
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public void Dispose() => DisposeSubscriptions();

    private async Task OnLoadedAsync(string pluginId)
    {
        var plugin = _plugins.GetPlugin(pluginId);
        if (plugin is not null)
            await TryRegisterAsync(plugin, CancellationToken.None).ConfigureAwait(false);
    }

    private async Task OnUnloadedAsync(string pluginId)
    {
        try
        {
            await _registrar.UnregisterAsync(pluginId, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _context.Logger.Error($"Failed to remove web contributions of plugin '{pluginId}': {ex.Message}", ex);
        }
    }

    private async Task TryRegisterAsync(IPlugin plugin, CancellationToken cancellationToken)
    {
        if (plugin is not IWebContributor contributor || _registrar.IsRegistered(plugin.Manifest.Id))
            return;

        var descriptor = new WebContributorDescriptor
        {
            Id = plugin.Manifest.Id,
            Name = plugin.Manifest.Name,
            Kind = "Plugin",
            Description = plugin.Manifest.Description ?? string.Empty
        };

        try
        {
            await _registrar.RegisterAsync(contributor, descriptor, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _context.Logger.Error($"Plugin '{descriptor.Id}' failed to register its web contributions: {ex.Message}", ex);
        }
    }

    private void DisposeSubscriptions()
    {
        foreach (var subscription in _subscriptions)
            subscription.Dispose();
        _subscriptions.Clear();
    }
}
