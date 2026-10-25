using Microsoft.AspNetCore.Http;

namespace CL.WebLogic.Hosting.Services;

/// <summary>
/// Renders error responses (4xx/5xx with no body) for the exception boundary. The default
/// renders ProblemDetails for APIs and a minimal HTML page otherwise; themes replace it to
/// render <c>errors/{status}.html</c>.
/// </summary>
public interface IWebErrorPageRenderer
{
    /// <summary>
    /// Writes the error response. <paramref name="exception"/> is set for unhandled errors and must
    /// never be shown outside Development.
    /// </summary>
    Task RenderAsync(HttpContext httpContext, int statusCode, Exception? exception, CancellationToken cancellationToken);
}
