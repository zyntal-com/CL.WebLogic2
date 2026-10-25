using System.Diagnostics;
using CL.WebLogic.Configuration.Services;
using CL.WebLogic.Hosting.Services;
using CL.WebLogic.Realtime.Models;
using Microsoft.AspNetCore.Http;

namespace CL.WebLogic.Realtime.Services;

/// <summary>
/// When <c>Realtime.BroadcastRequests</c> is on, publishes method, path (no query), status and
/// duration of each request to the Internal audience. Never client IPs or headers.
/// </summary>
public sealed class RealtimeRequestBroadcastMiddleware
{
    private readonly RequestDelegate _next;
    private readonly WebRealtimeHub _hub;
    private readonly WebLogicConfigHolder _config;
    private readonly IWebLogicContext _context;

    /// <summary>Creates the middleware.</summary>
    public RealtimeRequestBroadcastMiddleware(RequestDelegate next, WebRealtimeHub hub, WebLogicConfigHolder config, IWebLogicContext context)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _hub = hub ?? throw new ArgumentNullException(nameof(hub));
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <summary>Runs the request.</summary>
    public async Task InvokeAsync(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        var realtime = _config.Current.Realtime;
        var path = httpContext.Request.Path.Value ?? "/";
        if (!realtime.Enabled || !realtime.BroadcastRequests || path.StartsWith("/_weblogic/realtime", StringComparison.OrdinalIgnoreCase))
        {
            await _next(httpContext).ConfigureAwait(false);
            return;
        }

        var started = Stopwatch.GetTimestamp();
        try
        {
            await _next(httpContext).ConfigureAwait(false);
        }
        finally
        {
            try
            {
                var tenant = Security.Pipeline.WebSecurityContext.Get(httpContext)?.TenantId;
                var request = new
                {
                    method = httpContext.Request.Method,
                    path,
                    status = httpContext.Response.StatusCode,
                    durationMs = Math.Round(Stopwatch.GetElapsedTime(started).TotalMilliseconds, 1)
                };
                // A tenant's requests are only shown to that tenant's operators.
                await (tenant is null
                    ? _hub.PublishAsync("weblogic.request", request, WebRealtimeAudience.Internal)
                    : _hub.PublishToTenantAsync(tenant, "weblogic.request", request, WebRealtimeAudience.Internal)).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _context.Logger.Debug($"Request broadcast failed: {ex.Message}");
            }
        }
    }
}
