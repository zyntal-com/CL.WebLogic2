using CL.WebLogic.Routing.Models;
using CL.WebLogic.Runtime.Models;
using Microsoft.AspNetCore.Http;

namespace CL.WebLogic.Hosting.Services;

/// <summary>
/// Outermost WebLogic middleware. Tags every response with a correlation id, turns unhandled
/// exceptions into safe error responses, treats client disconnects as normal, and renders a
/// themed/ProblemDetails body for 4xx/5xx responses that were left empty.
/// </summary>
public sealed class ExceptionBoundaryMiddleware
{
    /// <summary>Status recorded (and logged) when the client disconnected mid-request.</summary>
    public const int ClientClosedRequest = 499;

    private readonly RequestDelegate _next;
    private readonly IWebLogicContext _context;
    private readonly IWebErrorPageRenderer _renderer;

    /// <summary>Creates the middleware.</summary>
    public ExceptionBoundaryMiddleware(RequestDelegate next, IWebLogicContext context, IWebErrorPageRenderer renderer)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
    }

    /// <summary>Runs the rest of the pipeline inside the boundary.</summary>
    public async Task InvokeAsync(HttpContext httpContext)
    {
        httpContext.Response.OnStarting(static state =>
        {
            var http = (HttpContext)state;
            http.Response.Headers["X-Correlation-Id"] = http.TraceIdentifier;
            return Task.CompletedTask;
        }, httpContext);

        try
        {
            await _next(httpContext).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (httpContext.RequestAborted.IsCancellationRequested)
        {
            _context.Logger.Debug($"Client disconnected: {httpContext.Request.Method} {httpContext.Request.Path} [{httpContext.TraceIdentifier}]");
            if (!httpContext.Response.HasStarted)
                httpContext.Response.StatusCode = ClientClosedRequest;
            return;
        }
        catch (WebRequestTooLargeException)
        {
            await RenderIfPossibleAsync(httpContext, StatusCodes.Status413PayloadTooLarge, null, clear: true).ConfigureAwait(false);
            return;
        }
        catch (BadHttpRequestException ex)
        {
            await RenderIfPossibleAsync(httpContext, ex.StatusCode, null, clear: true).ConfigureAwait(false);
            return;
        }
        catch (Exception ex)
        {
            _context.Logger.Error($"Unhandled exception for {httpContext.Request.Method} {httpContext.Request.Path} [{httpContext.TraceIdentifier}]: {ex.Message}", ex);
            if (httpContext.Response.HasStarted)
                throw; // Headers are gone; let the server abort the connection.

            await RenderIfPossibleAsync(httpContext, StatusCodes.Status500InternalServerError, ex, clear: true).ConfigureAwait(false);
            return;
        }

        if (httpContext.Response.StatusCode >= 400 && !httpContext.Response.HasStarted && WantsStatusPage(httpContext))
            await RenderIfPossibleAsync(httpContext, httpContext.Response.StatusCode, null, clear: false).ConfigureAwait(false);
    }

    /// <param name="httpContext">The current request.</param>
    /// <param name="statusCode">Status to render.</param>
    /// <param name="exception">The unhandled exception, if any (details shown only in Development).</param>
    /// <param name="clear">
    /// True on error paths: drop whatever the failed handler put on the response. False for an
    /// intentionally empty status response, whose headers (e.g. <c>Allow</c> on 405) must survive.
    /// </param>
    private async Task RenderIfPossibleAsync(HttpContext httpContext, int statusCode, Exception? exception, bool clear)
    {
        if (httpContext.Response.HasStarted)
            return;

        if (clear)
            httpContext.Response.Clear();
        httpContext.Response.StatusCode = statusCode;
        if (HttpMethods.IsHead(httpContext.Request.Method))
            return;

        try
        {
            await _renderer.RenderAsync(httpContext, statusCode, exception, httpContext.RequestAborted).ConfigureAwait(false);
        }
        catch (Exception renderError) when (renderError is not OperationCanceledException)
        {
            _context.Logger.Error($"Error page renderer failed [{httpContext.TraceIdentifier}]: {renderError.Message}", renderError);
        }
    }

    /// <summary>Static assets keep their bare status codes; everything else gets a body.</summary>
    private static bool WantsStatusPage(HttpContext httpContext) =>
        httpContext.GetEndpoint()?.Metadata.GetMetadata<WebRouteMetadata>()?.Kind != WebRouteKind.Asset;
}
