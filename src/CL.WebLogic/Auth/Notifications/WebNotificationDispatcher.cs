using System.Threading.Channels;
using CL.WebLogic.Auth.Users;

namespace CL.WebLogic.Auth.Notifications;

/// <summary>
/// Hands security e-mails to a background sender, so request timing does not reveal whether an
/// e-mail was sent (no account enumeration through password-reset timing) and a slow mail
/// server does not slow requests down. Link e-mails that anyone can request (reset, magic link,
/// verification, address change) are limited per kind and address, so a flood cannot spam a
/// mailbox or push security alerts out of the queue.
/// </summary>
public sealed class WebNotificationDispatcher
{
    /// <summary>Requested link e-mails allowed per kind and address within <see cref="RequestWindow"/>.</summary>
    public const int RequestsPerWindow = 3;

    /// <summary>The window for <see cref="RequestsPerWindow"/>.</summary>
    public static readonly TimeSpan RequestWindow = TimeSpan.FromMinutes(15);

    private static readonly HashSet<WebSecurityNotificationKind> Requested =
    [
        WebSecurityNotificationKind.PasswordReset, WebSecurityNotificationKind.MagicLink,
        WebSecurityNotificationKind.EmailVerification, WebSecurityNotificationKind.EmailChangeConfirmation
    ];

    private readonly Channel<WebSecurityNotification> _channel =
        Channel.CreateBounded<WebSecurityNotification>(new BoundedChannelOptions(1000) { FullMode = BoundedChannelFullMode.DropOldest, SingleReader = true });
    private readonly Security.Services.BoundedTtlCache<int> _requests;
    private readonly Lock _lock = new();

    /// <summary>Creates the dispatcher.</summary>
    public WebNotificationDispatcher(TimeProvider? time = null) =>
        _requests = new Security.Services.BoundedTtlCache<int>(50_000, time);

    internal ChannelReader<WebSecurityNotification> Reader => _channel.Reader;

    /// <summary>Notifications waiting to be sent (for the queue-length metric).</summary>
    internal int QueueLength => _channel.Reader.Count;

    /// <summary>Queues a notification (requested link e-mails over the per-address limit are dropped).</summary>
    public void Enqueue(WebSecurityNotification notification)
    {
        ArgumentNullException.ThrowIfNull(notification);
        if (Requested.Contains(notification.Kind))
        {
            var key = notification.Kind + ":" + notification.ToAddress.Trim().ToUpperInvariant();
            lock (_lock)
            {
                var count = _requests.TryGet(key, out var sent) ? sent : 0;
                if (count >= RequestsPerWindow)
                    return;
                _requests.Set(key, count + 1, RequestWindow);
            }
        }
        _channel.Writer.TryWrite(notification);
    }

    /// <summary>Queues a notification to <paramref name="user"/>'s address (skipped when the user has none).</summary>
    public void Enqueue(WebUser user, WebSecurityNotificationKind kind, string? link = null, string? toAddress = null, IReadOnlyDictionary<string, string>? values = null)
    {
        ArgumentNullException.ThrowIfNull(user);
        var to = toAddress ?? user.Email;
        if (string.IsNullOrWhiteSpace(to))
            return;
        Enqueue(new WebSecurityNotification
        {
            Kind = kind,
            ToAddress = to,
            UserId = user.Id,
            UserName = user.UserName,
            Link = link,
            Values = values ?? new Dictionary<string, string>()
        });
    }
}
