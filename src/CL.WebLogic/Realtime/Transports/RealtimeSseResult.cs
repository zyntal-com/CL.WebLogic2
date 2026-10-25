using System.Text;
using CL.WebLogic.Realtime.Models;
using CL.WebLogic.Realtime.Services;
using CL.WebLogic.Results;
using CL.WebLogic.Runtime.Models;
using Microsoft.AspNetCore.Http.Features;

namespace CL.WebLogic.Realtime.Transports;

/// <summary>Streams a connection's messages as server-sent events until the client leaves, the server closes it or its lifetime ends.</summary>
internal sealed class RealtimeSseResult : WebResult
{
    private readonly WebRealtimeHub _hub;
    private readonly WebRealtimeConnection _connection;
    private readonly string? _lastEventId;
    private readonly TimeSpan _heartbeat;
    private readonly TimeSpan _lifetime;
    private readonly TimeProvider _time;

    public RealtimeSseResult(WebRealtimeHub hub, WebRealtimeConnection connection, string? lastEventId, TimeSpan heartbeat, TimeSpan lifetime, TimeProvider time)
    {
        _hub = hub;
        _connection = connection;
        _lastEventId = lastEventId;
        _heartbeat = heartbeat;
        _lifetime = lifetime;
        _time = time;
    }

    public override async Task ExecuteAsync(WebRequestContext request, CancellationToken cancellationToken)
    {
        var response = request.HttpContext.Response;
        try
        {
            ApplyStatusAndHeaders(response);
            response.ContentType = "text/event-stream; charset=utf-8";
            response.Headers.CacheControl = "no-cache, no-transform";
            response.Headers["X-Accel-Buffering"] = "no";
            request.HttpContext.Features.Get<IHttpResponseBodyFeature>()?.DisableBuffering();

            using var lifetime = new CancellationTokenSource(_lifetime, _time);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _connection.Closed, lifetime.Token);
            var token = linked.Token;

            await WriteAsync(response, "retry: 3000\n: connected\n\n", token).ConfigureAwait(false);
            foreach (var envelope in _hub.Replay(_connection, _lastEventId))
                await WriteAsync(response, Format(envelope), token).ConfigureAwait(false);

            var reader = _connection.Reader;
            while (!token.IsCancellationRequested)
            {
                using (var beat = CancellationTokenSource.CreateLinkedTokenSource(token))
                {
                    beat.CancelAfter(_heartbeat);
                    try
                    {
                        if (!await reader.WaitToReadAsync(beat.Token).ConfigureAwait(false))
                            break;
                    }
                    catch (OperationCanceledException) when (!token.IsCancellationRequested)
                    {
                        await WriteAsync(response, ": ping\n\n", token).ConfigureAwait(false);
                        continue;
                    }
                }

                var batch = new StringBuilder();
                while (reader.TryRead(out var envelope))
                    batch.Append(Format(envelope));
                await WriteAsync(response, batch.ToString(), token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // Client left, server closed the connection, or its lifetime ended: the client reconnects.
        }
        finally
        {
            _hub.Disconnect(_connection);
            _connection.Dispose();
        }
    }

    internal static string Format(WebRealtimeEnvelope envelope) =>
        (envelope.Id.Length > 0 ? "id: " + envelope.Id + "\n" : "") + "data: " + envelope.Json + "\n\n";

    private static async Task WriteAsync(Microsoft.AspNetCore.Http.HttpResponse response, string text, CancellationToken cancellationToken)
    {
        await response.Body.WriteAsync(Encoding.UTF8.GetBytes(text), cancellationToken).ConfigureAwait(false);
        await response.Body.FlushAsync(cancellationToken).ConfigureAwait(false);
    }
}
