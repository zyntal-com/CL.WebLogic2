using CL.WebLogic.Auth.Events;
using CL.WebLogic.Configuration.Events;
using CL.WebLogic.Hosting.Services;
using CL.WebLogic.Realtime.Models;
using Microsoft.Extensions.Hosting;

namespace CL.WebLogic.Realtime.Services;

/// <summary>
/// Starts the realtime subscriptions: app bridges, closing a user's connections on sign-out and
/// password change (they reconnect with their new identity), and config notices to the Internal
/// audience (property names only, never values).
/// </summary>
public sealed class RealtimeStartupService : IHostedService
{
    private readonly WebRealtimeHub _hub;
    private readonly WebRealtimeBridge _bridge;
    private readonly IEnumerable<WebRealtimeBridgeRegistration> _registrations;
    private readonly IWebLogicContext _context;
    private readonly IHostApplicationLifetime? _lifetime;
    private readonly List<IDisposable> _subscriptions = [];

    /// <summary>Creates the service.</summary>
    public RealtimeStartupService(WebRealtimeHub hub, WebRealtimeBridge bridge, IEnumerable<WebRealtimeBridgeRegistration> registrations, IWebLogicContext context, IHostApplicationLifetime? lifetime = null)
    {
        _lifetime = lifetime;
        _hub = hub ?? throw new ArgumentNullException(nameof(hub));
        _bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
        _registrations = registrations ?? [];
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <inheritdoc/>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _subscriptions.Add(_context.Events.Subscribe<UserSignedOutEvent>(e => _hub.CloseUser(e.UserId)));
        _subscriptions.Add(_context.Events.Subscribe<PasswordChangedEvent>(e => _hub.CloseUser(e.UserId)));
        _subscriptions.Add(_context.Events.Subscribe<SessionEndedEvent>(e => _hub.CloseSession(e.IdHash)));
        _subscriptions.Add(_bridge.Register<WebLogicConfigAppliedEvent>("weblogic.config.applied", _ => WebRealtimeAudience.Internal, e => new { changed = e.ChangedProperties }));
        _subscriptions.Add(_bridge.Register<WebLogicRestartRequiredEvent>("weblogic.config.restart-required", _ => WebRealtimeAudience.Internal, e => new { changed = e.ChangedProperties }));
        foreach (var registration in _registrations)
            _subscriptions.Add(registration.Register(_bridge));
        // Streams never finish on their own: close them before the server waits for requests to drain.
        if (_lifetime is not null)
            _subscriptions.Add(_lifetime.ApplicationStopping.Register(_hub.CloseAll));
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task StopAsync(CancellationToken cancellationToken)
    {
        foreach (var subscription in _subscriptions)
            subscription.Dispose();
        _subscriptions.Clear();
        _hub.Dispose();
        return Task.CompletedTask;
    }
}
