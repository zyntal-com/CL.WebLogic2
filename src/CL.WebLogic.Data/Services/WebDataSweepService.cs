using CL.WebLogic.Hosting.Services;
using CL.WebLogic.Data.Models;
using CL.WebLogic.Data.Stores;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace CL.WebLogic.Data.Services;

/// <summary>
/// Deletes expired rows on <see cref="WebDataOptions.SweepInterval"/>: spent one-time keys, login
/// counters, idle rate-limit buckets and audit rows past <see cref="WebDataOptions.AuditRetention"/>.
/// (Expired sessions are swept by the toolkit's session sweeper.) Errors are logged, never thrown.
/// </summary>
public sealed class WebDataSweepService : BackgroundService
{
    private static readonly TimeSpan RateLimitIdle = TimeSpan.FromHours(1);
    private readonly IServiceProvider _services;
    private readonly WebDataOptions _options;
    private readonly IWebLogicContext _context;
    private readonly TimeProvider _time;

    /// <summary>Creates the service.</summary>
    public WebDataSweepService(IServiceProvider services, WebDataOptions options, IWebLogicContext context, TimeProvider? time = null)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _time = time ?? TimeProvider.System;
    }

    /// <summary>Runs one sweep now; returns the rows deleted.</summary>
    public async Task<int> SweepOnceAsync(CancellationToken cancellationToken)
    {
        var now = _time.GetUtcNow();
        var deleted = 0;
        if (_services.GetService<DataWebOneTimeUseStore>() is { } oneTime)
            deleted += await oneTime.DeleteExpiredAsync(now, cancellationToken).ConfigureAwait(false);
        if (_services.GetService<DataWebLoginThrottle>() is { } throttle)
            deleted += await throttle.DeleteExpiredAsync(now, cancellationToken).ConfigureAwait(false);
        if (_services.GetService<DataWebRateLimitStore>() is { } rateLimits)
            deleted += await rateLimits.DeleteIdleAsync(now, RateLimitIdle, cancellationToken).ConfigureAwait(false);
        if (_options.AuditRetention > TimeSpan.Zero && _services.GetService<DataWebAuditSink>() is { } audit)
            deleted += await audit.DeleteOlderThanAsync(now - _options.AuditRetention, cancellationToken).ConfigureAwait(false);
        return deleted;
    }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(_options.SweepInterval, _time);
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            try
            {
                var deleted = await SweepOnceAsync(stoppingToken).ConfigureAwait(false);
                if (deleted > 0)
                    _context.Logger.Debug($"WebLogic data sweep removed {deleted} expired rows.");
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _context.Logger.Warning($"WebLogic data sweep failed: {ex.Message}");
            }
        }
    }
}
