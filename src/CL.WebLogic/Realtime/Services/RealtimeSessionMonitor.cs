using CL.WebLogic.Configuration.Services;
using CL.WebLogic.Hosting.Services;
using CL.WebLogic.Security.Sessions;
using Microsoft.Extensions.Hosting;

namespace CL.WebLogic.Realtime.Services;

/// <summary>
/// Closes realtime connections whose session has ended. Every <c>Realtime.SessionCheckSeconds</c>
/// the sessions behind this node's open connections are looked up in the session store; a session
/// that was revoked (on any node), evicted, signed out or has expired closes its connections, and
/// the client reconnects with whatever identity it has now. Same-node revocations close at once
/// (<see cref="Auth.Events.SessionEndedEvent"/>); this check covers the rest.
/// </summary>
public sealed class RealtimeSessionMonitor : BackgroundService
{
    private static readonly TimeSpan IdlePoll = TimeSpan.FromSeconds(30);

    private readonly WebRealtimeHub _hub;
    private readonly IWebSessionStore _sessions;
    private readonly WebLogicConfigHolder _config;
    private readonly IWebLogicContext _context;
    private readonly TimeProvider _time;

    /// <summary>Creates the monitor.</summary>
    public RealtimeSessionMonitor(WebRealtimeHub hub, IWebSessionStore sessions, WebLogicConfigHolder config, IWebLogicContext context, TimeProvider? time = null)
    {
        _hub = hub ?? throw new ArgumentNullException(nameof(hub));
        _sessions = sessions ?? throw new ArgumentNullException(nameof(sessions));
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _time = time ?? TimeProvider.System;
    }

    /// <summary>Checks every open session-backed connection once; returns how many were closed.</summary>
    public async Task<int> CheckAsync(CancellationToken cancellationToken)
    {
        var now = _time.GetUtcNow();
        var closed = 0;
        var sessions = _hub.Connections.Select(c => c.Identity.SessionId).OfType<string>().Distinct(StringComparer.Ordinal).ToList();
        foreach (var idHash in sessions)
        {
            var record = await _sessions.GetAsync(idHash, cancellationToken).ConfigureAwait(false);
            var live = record is { AuthStage: WebSessionAuthStage.Authenticated } && record.ExpiresAt > now && record.AbsoluteExpiresAt > now;
            if (!live)
                closed += _hub.CloseSession(idHash);
        }
        if (closed > 0)
            _context.Logger.Debug($"Closed {closed} realtime connections whose session ended");
        return closed;
    }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                // Read every round, so a config change applies without a restart.
                var seconds = _config.Current.Realtime.SessionCheckSeconds;
                await Task.Delay(seconds > 0 ? TimeSpan.FromSeconds(seconds) : IdlePoll, _time, stoppingToken).ConfigureAwait(false);
                if (seconds <= 0 || !_config.Current.Realtime.Enabled || _hub.ConnectionCount == 0)
                    continue;
                try
                {
                    await CheckAsync(stoppingToken).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // Store unreachable: keep the connections and try again next round.
                    _context.Logger.Warning($"Realtime session check failed: {ex.Message}");
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Shutdown.
        }
    }
}
