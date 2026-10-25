using CL.WebLogic.Runtime.Models;
using Microsoft.AspNetCore.Http;

namespace CL.WebLogic.Results;

/// <summary>
/// An HTTP redirect. Create through <see cref="WebResult.Redirect"/> (local only) or
/// <see cref="WebResult.ExternalRedirect"/>. When the WebLogic client made the request, the
/// target is also announced in <c>X-WebLogic-Redirect</c> so the client can navigate.
/// </summary>
public sealed class RedirectResult : WebResult
{
    internal RedirectResult(string location, bool permanent, bool preserveMethod)
    {
        Location = location;
        Permanent = permanent;
        PreserveMethod = preserveMethod;
        StatusCode = (permanent, preserveMethod) switch
        {
            (true, true) => StatusCodes.Status308PermanentRedirect,
            (true, false) => StatusCodes.Status301MovedPermanently,
            (false, true) => StatusCodes.Status307TemporaryRedirect,
            _ => StatusCodes.Status302Found
        };
    }

    /// <summary>The redirect target.</summary>
    public string Location { get; }

    /// <summary>True for a permanent redirect.</summary>
    public bool Permanent { get; }

    /// <summary>True to keep the request method (307/308).</summary>
    public bool PreserveMethod { get; }

    /// <inheritdoc/>
    public override Task ExecuteAsync(WebRequestContext request, CancellationToken cancellationToken)
    {
        var response = request.HttpContext.Response;
        ApplyStatusAndHeaders(response);
        var location = Location.StartsWith('/') ? request.HttpContext.Request.PathBase + Location : Location;
        response.Headers.Location = location;
        if (request.IsWebLogicClient)
            response.Headers["X-WebLogic-Redirect"] = location;
        return Task.CompletedTask;
    }
}
