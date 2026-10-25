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
    /// invoke the handler and execute the returned result.
    /// </summary>
    public static RequestDelegate Create(WebRouteDefinition route)
    {
        var pipeline = BuildPipeline(route);

        return async httpContext =>
        {
            var request = new WebRequestContext(httpContext, route.Metadata, route.JsonOptions);
            httpContext.Features.Set(request); // lets template-time features (widgets, forms) reach the route context
            var cancellationToken = httpContext.RequestAborted;

            var result = await pipeline(request, cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException($"Route '{route.Metadata.Pattern}' returned no result.");

            await result.ExecuteAsync(request, cancellationToken).ConfigureAwait(false);
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
