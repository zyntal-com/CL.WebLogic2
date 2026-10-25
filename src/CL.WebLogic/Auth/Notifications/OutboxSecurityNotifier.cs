using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using CL.WebLogic.Configuration.Services;
using CL.WebLogic.Hosting.Services;
using CL.WebLogic.Security.Services;

namespace CL.WebLogic.Auth.Notifications;

/// <summary>
/// Development notifier: writes each e-mail as an <c>.eml</c> file to the outbox folder under
/// the WebLogic data directory and keeps the last 200 in <see cref="Messages"/>.
/// </summary>
public sealed class OutboxSecurityNotifier : IWebSecurityNotifier, IWebDevelopmentStore
{
    private const int Keep = 200;

    private readonly ConcurrentQueue<WebSecurityNotification> _messages = new();
    private readonly WebLogicConfigHolder _config;
    private readonly IWebLogicContext _context;
    private readonly TimeProvider _time;

    /// <summary>Creates the notifier.</summary>
    public OutboxSecurityNotifier(WebLogicConfigHolder config, IWebLogicContext context, TimeProvider? time = null)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _time = time ?? TimeProvider.System;
    }

    /// <summary>The most recent notifications, oldest first.</summary>
    public IReadOnlyList<WebSecurityNotification> Messages => [.. _messages];

    /// <inheritdoc/>
    public async Task SendAsync(WebSecurityNotification notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);
        _messages.Enqueue(notification);
        while (_messages.Count > Keep)
            _messages.TryDequeue(out _);

        var config = _config.Current;
        var (subject, text, _) = SecurityNotificationText.Render(notification, config.SiteName);
        var folder = Path.Combine(_context.DataDirectory, config.Auth.Notifications.OutboxFolder);
        Directory.CreateDirectory(folder);

        var now = _time.GetUtcNow();
        var file = Path.Combine(folder, now.ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture) + "-" + notification.Kind + "-" + Guid.NewGuid().ToString("N")[..8] + ".eml");
        var eml = new StringBuilder()
            .Append("From: ").Append(Header(config.Auth.Notifications.FromAddress)).Append("\r\n")
            .Append("To: ").Append(Header(notification.ToAddress)).Append("\r\n")
            .Append("Subject: ").Append(Header(subject)).Append("\r\n")
            .Append("Date: ").Append(now.ToString("r", CultureInfo.InvariantCulture)).Append("\r\n")
            .Append("X-WebLogic-Kind: ").Append(notification.Kind).Append("\r\n")
            .Append("Content-Type: text/plain; charset=utf-8\r\n\r\n")
            .Append(text.Replace("\n", "\r\n", StringComparison.Ordinal))
            .ToString();
        await File.WriteAllTextAsync(file, eml, cancellationToken).ConfigureAwait(false);
        _context.Logger.Info($"Outbox: {notification.Kind} e-mail for user {notification.UserId} written to {Path.GetFileName(file)}");
    }

    private static string Header(string value) => value.Replace("\r", "", StringComparison.Ordinal).Replace("\n", "", StringComparison.Ordinal);
}
