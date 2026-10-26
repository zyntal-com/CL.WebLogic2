using CL.WebLogic.Auth.Events;
using CL.WebLogic.Auth.Notifications;
using CL.WebLogic.Auth.Users;
using CL.WebLogic.Configuration.Services;
using CL.WebLogic.Hosting.Services;
using CL.WebLogic.Security.Sessions;

namespace CL.WebLogic.Auth.Sessions;

/// <summary>
/// A user's sessions as devices: listing and revoking them, the per-user session cap
/// (<c>Session.MaxPerUser</c>) and new-device e-mails (<c>Auth.Notifications.NotifyNewDevice</c>).
/// Everything here needs a session store implementing <see cref="IWebSessionListStore"/>.
/// </summary>
public sealed class WebSessionDevices
{
    /// <summary>The device name in a new-device mail when the user agent is not recognised (the raw header is never mailed).</summary>
    internal const string UnknownDevice = "an unknown browser";

    private readonly IWebSessionStore _store;
    private readonly WebLogicConfigHolder _config;
    private readonly WebNotificationDispatcher _notifications;
    private readonly IWebLogicContext _context;
    private readonly TimeProvider _time;
    private int _warned;

    /// <summary>Creates the service.</summary>
    public WebSessionDevices(IWebSessionStore store, WebLogicConfigHolder config, WebNotificationDispatcher notifications, IWebLogicContext context, TimeProvider? time = null)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _notifications = notifications ?? throw new ArgumentNullException(nameof(notifications));
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _time = time ?? TimeProvider.System;
    }

    /// <summary>True when the session store can list a user's sessions.</summary>
    public bool IsSupported => _store is IWebSessionListStore;

    /// <summary>The live sessions of <paramref name="userId"/>, newest activity first; <paramref name="currentIdHash"/> is flagged.</summary>
    public async Task<IReadOnlyList<WebSessionInfo>> ListAsync(string userId, string? currentIdHash, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(userId);
        var sessions = await LiveSessionsAsync(userId, cancellationToken).ConfigureAwait(false);
        return [.. sessions
            .OrderByDescending(s => s.LastSeenAt)
            .Select(s => new WebSessionInfo
            {
                IdHash = s.IdHash,
                CreatedAt = s.CreatedAt,
                LastSeenAt = s.LastSeenAt,
                AuthenticatedAt = s.AuthenticatedAt,
                UserAgent = s.UserAgent,
                Device = WebUserAgent.Describe(s.UserAgent),
                Persistent = s.Persistent,
                AuthenticationMethods = s.AuthenticationMethods,
                IsCurrent = string.Equals(s.IdHash, currentIdHash, StringComparison.Ordinal)
            })];
    }

    /// <summary>Ends one of <paramref name="userId"/>'s sessions. False when it does not exist or belongs to someone else.</summary>
    public async Task<bool> RevokeAsync(string userId, string idHash, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(userId);
        if (string.IsNullOrEmpty(idHash) || await _store.GetAsync(idHash, cancellationToken).ConfigureAwait(false) is not { } record
            || !string.Equals(record.UserId, userId, StringComparison.Ordinal))
            return false;
        await _store.DeleteAsync(idHash, cancellationToken).ConfigureAwait(false);
        _context.Events.Publish(new SessionEndedEvent(userId, idHash));
        return true;
    }

    /// <summary>
    /// Runs before a fresh sign-in of <paramref name="user"/>: ends the oldest sessions beyond
    /// <c>Session.MaxPerUser</c> and e-mails a new-device notice when enabled.
    /// <paramref name="currentIdHash"/> (the session being replaced) is ignored.
    /// </summary>
    public async Task OnSigningInAsync(WebUser user, string? currentIdHash, string? userAgent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        var max = _config.Current.Security.Session.MaxPerUser;
        var notify = _config.Current.Auth.Notifications.NotifyNewDevice;
        if (max == 0 && !notify)
            return;
        if (!IsSupported)
        {
            if (Interlocked.Exchange(ref _warned, 1) == 0)
                _context.Logger.Warning($"Session.MaxPerUser / NotifyNewDevice need a session store implementing {nameof(IWebSessionListStore)}; {_store.GetType().Name} does not, so they are skipped.");
            return;
        }

        var others = (await LiveSessionsAsync(user.Id, cancellationToken).ConfigureAwait(false))
            .Where(s => !string.Equals(s.IdHash, currentIdHash, StringComparison.Ordinal))
            .ToList();

        // Same browser and OS family = same device, so a browser update is not a "new device".
        var device = WebUserAgent.DeviceKey(userAgent);
        if (notify && device.Length > 0 && others.All(s => !string.Equals(WebUserAgent.DeviceKey(s.UserAgent), device, StringComparison.Ordinal)))
            _notifications.Enqueue(user, WebSecurityNotificationKind.NewDeviceSignIn, values: new Dictionary<string, string> { ["device"] = WebUserAgent.Describe(userAgent) ?? UnknownDevice });

        // The new session counts too, so keep at most max - 1 of the others.
        if (max > 0 && others.Count >= max)
        {
            foreach (var oldest in others.OrderBy(s => s.CreatedAt).Take(others.Count - (max - 1)))
            {
                await _store.DeleteAsync(oldest.IdHash, cancellationToken).ConfigureAwait(false);
                _context.Events.Publish(new SessionEndedEvent(user.Id, oldest.IdHash));
            }
        }
    }

    private async Task<List<WebSessionRecord>> LiveSessionsAsync(string userId, CancellationToken cancellationToken)
    {
        if (_store is not IWebSessionListStore list)
            return [];
        var now = _time.GetUtcNow();
        return [.. (await list.ListForUserAsync(userId, cancellationToken).ConfigureAwait(false))
            .Where(s => s.AuthStage == WebSessionAuthStage.Authenticated && s.ExpiresAt > now && s.AbsoluteExpiresAt > now)];
    }
}
