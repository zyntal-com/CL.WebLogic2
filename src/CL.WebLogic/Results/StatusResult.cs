using CL.WebLogic.Runtime.Models;

namespace CL.WebLogic.Results;

/// <summary>An empty response that only carries a status code (and optional headers).</summary>
public sealed class StatusResult : WebResult
{
    /// <inheritdoc/>
    public override Task ExecuteAsync(WebRequestContext request, CancellationToken cancellationToken)
    {
        ApplyStatusAndHeaders(request.HttpContext.Response);
        return Task.CompletedTask;
    }
}
