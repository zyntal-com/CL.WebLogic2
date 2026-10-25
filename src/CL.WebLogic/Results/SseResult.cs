using CL.WebLogic.Runtime.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;

namespace CL.WebLogic.Results;

/// <summary>
/// Streams server-sent events until the source completes or the client disconnects.
/// Response buffering and compression are disabled so each event is flushed immediately.
/// </summary>
public sealed class SseResult : WebResult
{
    /// <summary>Creates the result.</summary>
    public SseResult(IAsyncEnumerable<WebSseEvent> events) => Events = events ?? throw new ArgumentNullException(nameof(events));

    /// <summary>The event source.</summary>
    public IAsyncEnumerable<WebSseEvent> Events { get; }

    /// <inheritdoc/>
    public override async Task ExecuteAsync(WebRequestContext request, CancellationToken cancellationToken)
    {
        var http = request.HttpContext;
        var response = http.Response;
        ApplyStatusAndHeaders(response);
        response.ContentType = "text/event-stream; charset=utf-8";
        response.Headers.CacheControl = "no-cache, no-store";
        response.Headers["X-Accel-Buffering"] = "no";
        http.Features.Get<IHttpResponseBodyFeature>()?.DisableBuffering();

        await response.WriteAsync(WebSseWriter.Comment("connected"), cancellationToken).ConfigureAwait(false);
        await response.Body.FlushAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            await foreach (var item in Events.WithCancellation(cancellationToken).ConfigureAwait(false))
            {
                await response.WriteAsync(WebSseWriter.Format(item), cancellationToken).ConfigureAwait(false);
                await response.Body.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Client went away — a normal way for a stream to end.
        }
    }
}
