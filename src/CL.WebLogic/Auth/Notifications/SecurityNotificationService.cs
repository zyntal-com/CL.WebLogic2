using CL.WebLogic.Hosting.Services;
using Microsoft.Extensions.Hosting;

namespace CL.WebLogic.Auth.Notifications;

/// <summary>Sends queued security e-mails through the registered <see cref="IWebSecurityNotifier"/>.</summary>
public sealed class SecurityNotificationService : BackgroundService
{
    private readonly WebNotificationDispatcher _queue;
    private readonly IWebSecurityNotifier _notifier;
    private readonly IWebLogicContext _context;

    /// <summary>Creates the service.</summary>
    public SecurityNotificationService(WebNotificationDispatcher queue, IWebSecurityNotifier notifier, IWebLogicContext context)
    {
        _queue = queue ?? throw new ArgumentNullException(nameof(queue));
        _notifier = notifier ?? throw new ArgumentNullException(nameof(notifier));
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var notification in _queue.Reader.ReadAllAsync(stoppingToken).ConfigureAwait(false))
            {
                try
                {
                    await _notifier.SendAsync(notification, stoppingToken).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _context.Logger.Error($"Sending the {notification.Kind} e-mail for user {notification.UserId} failed: {ex.Message}", ex);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Shutting down.
        }
    }
}
