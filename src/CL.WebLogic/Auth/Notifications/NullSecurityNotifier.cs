using CL.WebLogic.Hosting.Services;

namespace CL.WebLogic.Auth.Notifications;

/// <summary>Default notifier: sends nothing and logs a warning (without the link).</summary>
public sealed class NullSecurityNotifier : IWebSecurityNotifier
{
    private readonly IWebLogicContext _context;

    /// <summary>Creates the notifier.</summary>
    public NullSecurityNotifier(IWebLogicContext context) => _context = context ?? throw new ArgumentNullException(nameof(context));

    /// <inheritdoc/>
    public Task SendAsync(WebSecurityNotification notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);
        _context.Logger.Warning($"No security notifier is registered; the {notification.Kind} e-mail for user {notification.UserId} was not sent. Use web.UseMailNotifications() or web.UseSecurityNotifier<T>().");
        return Task.CompletedTask;
    }
}
