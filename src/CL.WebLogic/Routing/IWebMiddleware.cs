using CL.WebLogic.Results;
using CL.WebLogic.Runtime.Models;

namespace CL.WebLogic.Routing;

/// <summary>Continues the route middleware chain (eventually invoking the handler).</summary>
public delegate ValueTask<WebResult> WebMiddlewareNext(WebRequestContext request, CancellationToken cancellationToken);

/// <summary>
/// Route-scoped middleware. Runs after the security pipeline (authentication, authorization
/// and CSRF have already passed) and wraps the route handler.
/// </summary>
public interface IWebMiddleware
{
    /// <summary>Runs the middleware; call <paramref name="continuation"/> to continue the chain.</summary>
    ValueTask<WebResult> InvokeAsync(WebRequestContext request, WebMiddlewareNext continuation, CancellationToken cancellationToken);
}
