using Microsoft.AspNetCore.Http;

namespace CL.WebLogic.Templates.Rendering;

/// <summary>
/// Async work done before a request-bound render (templates themselves run synchronously where
/// possible), e.g. resolving the user's permissions for <c>{if:permission:…}</c>.
/// </summary>
public interface ITemplateRenderPreparer
{
    /// <summary>Prepares the request for rendering.</summary>
    ValueTask PrepareAsync(HttpContext httpContext, CancellationToken cancellationToken);
}
