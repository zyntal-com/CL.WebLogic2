using CL.WebLogic.Results;
using CL.WebLogic.Runtime.Models;

namespace CL.WebLogic.Routing.Models;

/// <summary>Handles a request to a WebLogic route and produces a <see cref="WebResult"/>.</summary>
/// <param name="request">The per-request context.</param>
/// <param name="cancellationToken">Signalled when the client disconnects.</param>
public delegate ValueTask<WebResult> WebRouteHandler(WebRequestContext request, CancellationToken cancellationToken);
