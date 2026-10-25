using CL.WebLogic.Security.Pipeline;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Http.Metadata;

namespace CL.WebLogic.Security.Stages;

/// <summary>
/// Applies the body limit (route <c>WithBodyLimit</c>, <c>[RequestSizeLimit]</c>, or the default),
/// refusing oversized declared bodies with 413, and bounds form field counts.
/// </summary>
public sealed class RequestLimitsStage : IWebSecurityStage
{
    /// <inheritdoc/>
    public string Name => "request-limits";

    /// <inheritdoc/>
    public WebSecurityPhase Phase => WebSecurityPhase.Endpoint;

    /// <inheritdoc/>
    public int Order => 600;

    /// <inheritdoc/>
    public ValueTask<WebSecurityDecision> ExecuteAsync(WebSecurityContext context, CancellationToken cancellationToken)
    {
        var httpContext = context.HttpContext;
        var limits = context.Settings.Config.Limits;
        var limit = context.Route?.MaxBodyBytes
            ?? httpContext.GetEndpoint()?.Metadata.GetMetadata<IRequestSizeLimitMetadata>()?.MaxRequestBodySize
            ?? limits.MaxBodyBytes;

        if (httpContext.Request.ContentLength > limit)
            return ValueTask.FromResult(WebSecurityDecision.Reject(StatusCodes.Status413PayloadTooLarge, "body-too-large"));

        var sizeFeature = httpContext.Features.Get<IHttpMaxRequestBodySizeFeature>();
        if (sizeFeature is { IsReadOnly: false })
            sizeFeature.MaxRequestBodySize = limit;

        if (httpContext.Request.HasFormContentType && httpContext.Features.Get<IFormFeature>() is null)
        {
            httpContext.Features.Set<IFormFeature>(new FormFeature(httpContext.Request, new FormOptions
            {
                ValueCountLimit = limits.MaxFormFields,
                MultipartBodyLengthLimit = limit
            }));
        }

        return ValueTask.FromResult(WebSecurityDecision.Continue);
    }
}
