using CL.WebLogic.Configuration.Services;
using CL.WebLogic.Realtime.Services;
using CL.WebLogic.Security.Pipeline;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Builder;

namespace CL.WebLogic.Hosting.Services;

/// <summary>Adds WebLogic's middleware to the request pipeline.</summary>
public static class WebLogicApplicationBuilderExtensions
{
    /// <summary>
    /// Adds, in order: the exception boundary, the realtime request broadcaster, WebSockets, the
    /// pre-routing security stages (network, host, transport, throttling, authentication), routing,
    /// CORS, the endpoint security stages (limits, route throttling, authorization, CSRF), ASP.NET
    /// authorization for minimal APIs and the output cache. Call before <c>MapCodeLogicWebLogic()</c>.
    /// </summary>
    public static IApplicationBuilder UseCodeLogicWebLogic(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var config = app.ApplicationServices.GetRequiredService<WebLogicConfigHolder>().Current;
        app.UseMiddleware<ExceptionBoundaryMiddleware>();
        app.UseMiddleware<RealtimeRequestBroadcastMiddleware>();
        app.UseWebSockets(new WebSocketOptions { KeepAliveInterval = TimeSpan.FromSeconds(config.Realtime.HeartbeatSeconds) });
        app.UseMiddleware<WebSecurityMiddleware>();
        app.UseRouting();
        app.UseCors();
        app.UseMiddleware<WebEndpointSecurityMiddleware>();
        app.UseAuthorization();
        // After endpoint security, so a cache hit can never skip authorization.
        if (config.Cache.OutputCache)
            app.UseOutputCache();
        return app;
    }
}
