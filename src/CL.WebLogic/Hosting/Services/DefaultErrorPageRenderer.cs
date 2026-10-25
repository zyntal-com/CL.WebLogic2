using System.Text.Encodings.Web;
using CL.WebLogic.Results;
using CL.WebLogic.Routing.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Hosting;

namespace CL.WebLogic.Hosting.Services;

/// <summary>
/// Default error renderer: RFC 7807 ProblemDetails for API / machine clients, a small
/// self-contained HTML page for browsers. Exception details appear only in Development.
/// </summary>
public sealed class DefaultErrorPageRenderer : IWebErrorPageRenderer
{
    private readonly IHostEnvironment _environment;

    /// <summary>Creates the renderer.</summary>
    public DefaultErrorPageRenderer(IHostEnvironment environment) =>
        _environment = environment ?? throw new ArgumentNullException(nameof(environment));

    /// <inheritdoc/>
    public async Task RenderAsync(HttpContext httpContext, int statusCode, Exception? exception, CancellationToken cancellationToken)
    {
        var showDetails = exception is not null && _environment.IsDevelopment();

        if (WantsProblemJson(httpContext))
        {
            var problem = new ProblemDetails { Status = statusCode, Detail = showDetails ? exception!.Message : null };
            await ProblemResult.WriteAsync(httpContext, problem, cancellationToken).ConfigureAwait(false);
            return;
        }

        httpContext.Response.StatusCode = statusCode;
        httpContext.Response.ContentType = "text/html; charset=utf-8";
        await httpContext.Response.WriteAsync(BuildHtml(httpContext, statusCode, showDetails ? exception : null), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>True for API routes and for clients that ask for JSON rather than HTML.</summary>
    internal static bool WantsProblemJson(HttpContext httpContext)
    {
        var kind = httpContext.GetEndpoint()?.Metadata.GetMetadata<WebRouteMetadata>()?.Kind;
        if (kind is WebRouteKind.Api or WebRouteKind.Sse or WebRouteKind.System)
            return true;

        var accept = httpContext.Request.Headers.Accept.ToString();
        return (accept.Contains("application/json", StringComparison.OrdinalIgnoreCase)
                || accept.Contains("application/problem+json", StringComparison.OrdinalIgnoreCase))
            && !accept.Contains("text/html", StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildHtml(HttpContext httpContext, int statusCode, Exception? exception)
    {
        var encoder = HtmlEncoder.Default;
        var title = encoder.Encode($"{statusCode} {ReasonPhrases.GetReasonPhrase(statusCode)}");
        var trace = encoder.Encode(httpContext.TraceIdentifier);
        var details = exception is null
            ? string.Empty
            : $"<pre>{encoder.Encode(exception.ToString())}</pre>";

        return $"""
            <!DOCTYPE html>
            <html lang="en">
            <head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1"><title>{title}</title></head>
            <body style="font-family:system-ui,sans-serif;max-width:40rem;margin:4rem auto;padding:0 1rem">
            <h1>{title}</h1>
            <p>Reference: <code>{trace}</code></p>
            {details}
            </body>
            </html>
            """;
    }
}
