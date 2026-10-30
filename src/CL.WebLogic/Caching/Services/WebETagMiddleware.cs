using System.Buffers.Text;
using System.Security.Cryptography;
using CL.WebLogic.Configuration.Models;
using CL.WebLogic.Configuration.Services;
using CL.WebLogic.Routing.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;

namespace CL.WebLogic.Caching.Services;

/// <summary>
/// Conditional GET for pages and JSON. For page, fragment, API and fallback routes with ETags on
/// (<c>WithETag()</c> or <c>Cache.ETags</c>), buffers the body of GET/HEAD requests, and when the
/// answer is a non-personal 200 without its own ETag, adds a weak ETag (truncated SHA-256 of
/// the uncompressed body, so it fits every encoding) and <c>Cache-Control: no-cache</c> when none is set. A matching <c>If-None-Match</c>
/// gets a 304 without a body. Personal responses (<see cref="WebResponsePrivacy"/>) never get one.
/// </summary>
/// <remarks>
/// Sits after endpoint security and in front of the output cache, so cached pages get ETags too
/// and a 304 is only sent to callers that passed authorization.
/// </remarks>
internal sealed class WebETagMiddleware
{
    private readonly RequestDelegate _next;
    private readonly WebLogicConfigHolder _config;

    /// <summary>Creates the middleware.</summary>
    public WebETagMiddleware(RequestDelegate next, WebLogicConfigHolder config)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _config = config ?? throw new ArgumentNullException(nameof(config));
    }

    /// <summary>Runs the request.</summary>
    public async Task InvokeAsync(HttpContext context)
    {
        var config = _config.Current;
        if (!Applies(context, config))
        {
            await _next(context).ConfigureAwait(false);
            return;
        }

        var original = context.Features.Get<IHttpResponseBodyFeature>()!;
        var buffer = new WebETagBufferStream(original, config.Cache.ETagMaxBytes);
        try
        {
            var feature = new StreamResponseBodyFeature(buffer, original);
            context.Features.Set<IHttpResponseBodyFeature>(feature);
            try
            {
                await _next(context).ConfigureAwait(false);
                await feature.CompleteAsync().ConfigureAwait(false);
            }
            finally
            {
                context.Features.Set(original);
            }

            if (!buffer.PassedThrough)
                await FinishAsync(context, buffer.Buffered, config).ConfigureAwait(false);
        }
        finally
        {
            await buffer.DisposeAsync().ConfigureAwait(false);
        }
    }

    private static bool Applies(HttpContext context, WebLogicConfig config)
    {
        var method = context.Request.Method;
        if (!HttpMethods.IsGet(method) && !HttpMethods.IsHead(method))
            return false;
        if (context.GetEndpoint()?.Metadata.GetMetadata<WebRouteMetadata>() is not { } route
            || route.Kind is not (WebRouteKind.Page or WebRouteKind.Fragment or WebRouteKind.Api or WebRouteKind.Fallback))
            return false;
        return (route.ETag ?? config.Cache.ETags) && !WebResponsePrivacy.IsPersonalRequest(context, config);
    }

    private static async Task FinishAsync(HttpContext context, ReadOnlyMemory<byte> body, WebLogicConfig config)
    {
        var response = context.Response;
        if (IsTaggable(context, body, config))
        {
            var tag = Compute(body.Span);
            response.Headers.ETag = tag;
            if (StringValues.IsNullOrEmpty(response.Headers.CacheControl))
                response.Headers.CacheControl = "no-cache";
            if (Matches(context.Request.Headers.IfNoneMatch, tag))
            {
                response.StatusCode = StatusCodes.Status304NotModified;
                response.ContentLength = null;
                return;
            }
        }

        if (body.IsEmpty)
            return;
        if (!response.HasStarted && response.ContentLength is null)
            response.ContentLength = body.Length;
        await response.Body.WriteAsync(body, context.RequestAborted).ConfigureAwait(false);
    }

    private static bool IsTaggable(HttpContext context, ReadOnlyMemory<byte> body, WebLogicConfig config)
    {
        var response = context.Response;
        if (response.HasStarted || response.StatusCode != StatusCodes.Status200OK)
            return false;
        if (response.Headers.ContainsKey(HeaderNames.ETag) || response.Headers.ContainsKey(HeaderNames.ContentEncoding))
            return false;
        // A HEAD handler that skipped the body would hash to the empty ETag.
        if (body.IsEmpty && HttpMethods.IsHead(context.Request.Method))
            return false;
        return !WebResponsePrivacy.IsPersonal(context, config);
    }

    private static string Compute(ReadOnlySpan<byte> body)
    {
        Span<byte> hash = stackalloc byte[SHA256.HashSizeInBytes];
        SHA256.HashData(body, hash);
        return "W/\"" + Base64Url.EncodeToString(hash[..16]) + "\"";
    }

    private static bool Matches(StringValues ifNoneMatch, string tag)
    {
        if (StringValues.IsNullOrEmpty(ifNoneMatch) || !EntityTagHeaderValue.TryParseList(ifNoneMatch, out var candidates))
            return false;
        var current = EntityTagHeaderValue.Parse(tag);
        return candidates.Any(c => c.Equals(EntityTagHeaderValue.Any) || c.Compare(current, useStrongComparison: false));
    }
}
