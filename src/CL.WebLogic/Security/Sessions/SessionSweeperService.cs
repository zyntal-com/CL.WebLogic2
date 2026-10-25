using CL.WebLogic.Hosting.Services;
using Microsoft.Extensions.Hosting;

namespace CL.WebLogic.Security.Sessions;

/// <summary>Deletes expired sessions every five minutes.</summary>
public sealed class SessionSweeperService : BackgroundService
{
    private readonly IWebSessionStore _store;
    private readonly IWebLogicContext _context;
    private readonly TimeProvider _time;

    /// <summary>Creates the service.</summary>
    public SessionSweeperService(IWebSessionStore store, IWebLogicContext context, TimeProvider? time = null)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _time = time ?? TimeProvider.System;
    }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(5), _time);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
            {
                try
                {
                    var removed = await _store.DeleteExpiredAsync(_time.GetUtcNow(), stoppingToken).ConfigureAwait(false);
                    if (removed > 0)
                        _context.Logger.Debug($"Removed {removed} expired sessions");
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _context.Logger.Error($"Session sweep failed: {ex.Message}", ex);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Shutdown.
        }
    }
}
