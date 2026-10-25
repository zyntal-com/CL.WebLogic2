namespace CL.WebLogic.Auth.Notifications;

/// <summary>
/// Delivers security e-mails. Built in: <see cref="NullSecurityNotifier"/> (default: logs),
/// <see cref="OutboxSecurityNotifier"/> (development: .eml files) and
/// <see cref="MailSecurityNotifier"/> (CL.Mail).
/// </summary>
public interface IWebSecurityNotifier
{
    /// <summary>Sends the notification.</summary>
    Task SendAsync(WebSecurityNotification notification, CancellationToken cancellationToken);
}
