using System.Diagnostics;
using CL.WebLogic.Diagnostics.Services;
using CL.WebLogic.Results;
using CL.WebLogic.Routing;
using CL.WebLogic.Routing.Models;
using CL.WebLogic.Runtime.Models;
using Microsoft.AspNetCore.Http;

namespace CL.WebLogic.Hosting.Services;

/// <summary>Builds the ASP.NET <see cref="RequestDelegate"/> that runs a WebLogic route.</summary>
internal static class WebRouteEndpointInvoker
{
    /// <summary>
    /// Creates the delegate: build the request context, run route middleware (outermost first),
    /// invoke the handler and execute the returned result. With <paramref name="metrics"/>, the
    /// request is counted and timed while a listener measures the route instruments.
    /// </summary>
    public static RequestDelegate Create(WebRouteDefinition route, WebLogicMetrics? metrics = null)
    {
        var pipeline = BuildPipeline(route);

        async Task RunAsync(HttpContext httpContext)
        {
            var request = new WebRequestContext(httpContext, route.Metadata, route.JsonOptions);
            httpContext.Features.Set(request); // lets template-time features (widgets, forms) reach the route context
            var cancellationToken = httpContext.RequestAborted;

            var result = await pipeline(request, cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException($"Route '{route.Metadata.Pattern}' returned no result.");

            await result.ExecuteAsync(request, cancellationToken).ConfigureAwait(false);
        }

        if (metrics is null)
            return RunAsync;

        return async httpContext =>
        {
            if (!metrics.RouteMetricsEnabled)
            {
                await RunAsync(httpContext).ConfigureAwait(false);
                return;
            }

            var started = Stopwatch.GetTimestamp();
            var failed = false;
            try
            {
                await RunAsync(httpContext).ConfigureAwait(false);
            }
            catch
            {
                failed = true; // the exception boundary turns it into a 500
                throw;
            }
            finally
            {
                metrics.RecordRequest(route.Metadata.Pattern, httpContext.Request.Method,
                    failed ? StatusCodes.Status500InternalServerError : httpContext.Response.StatusCode,
                    Stopwatch.GetElapsedTime(started));
            }
        };
    }

    private static WebMiddlewareNext BuildPipeline(WebRouteDefinition route)
    {
        WebMiddlewareNext next = (request, cancellationToken) => route.Handler(request, cancellationToken);

        for (var i = route.Middleware.Count - 1; i >= 0; i--)
        {
            var middleware = route.Middleware[i];
            var inner = next;
            next = (request, cancellationToken) => middleware.InvokeAsync(request, inner, cancellationToken);
        }

        return next;
    }
}
