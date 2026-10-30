using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using CL.WebLogic.Hosting.Services;
using CL.WebLogic.Realtime.Models;
using CL.WebLogic.Realtime.Services;
using CL.WebLogic.Results;
using CL.WebLogic.Runtime.Models;
using Microsoft.Extensions.DependencyInjection;

namespace CL.WebLogic.Realtime.Transports;

/// <summary>Accepts the WebSocket and pumps the connection's messages; client messages go to <see cref="IWebRealtimeReceiver"/>.</summary>
internal sealed class RealtimeWebSocketResult : WebResult
{
    private const int MaxMessageBytes = 16 * 1024;
    private static readonly byte[] Pong = Encoding.UTF8.GetBytes("{\"type\":\"pong\"}");

    private readonly WebRealtimeHub _hub;
    private readonly WebRealtimeConnection _connection;
    private readonly string? _lastEventId;
    private readonly TimeSpan _lifetime;
    private readonly TimeProvider _time;
    private readonly IWebLogicContext _context;
    private readonly int _messagesPerMinute;

    public RealtimeWebSocketResult(WebRealtimeHub hub, WebRealtimeConnection connection, string? lastEventId, TimeSpan lifetime, TimeProvider time, IWebLogicContext context,
        int messagesPerMinute)
    {
        _messagesPerMinute = Math.Max(1, messagesPerMinute);
        _hub = hub;
        _connection = connection;
        _lastEventId = lastEventId;
        _lifetime = lifetime;
        _time = time;
        _context = context;
    }

    public override async Task ExecuteAsync(WebRequestContext request, CancellationToken cancellationToken)
    {
        WebSocket? socket = null;
        using var send = new SemaphoreSlim(1, 1);
        try
        {
            socket = await request.HttpContext.WebSockets.AcceptWebSocketAsync().ConfigureAwait(false);
            using var lifetime = new CancellationTokenSource(_lifetime, _time);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _connection.Closed, lifetime.Token);
            var receiver = request.HttpContext.RequestServices.GetService<IWebRealtimeReceiver>();

            var sending = SendLoopAsync(socket, send, linked.Token);
            var receiving = ReceiveLoopAsync(socket, send, receiver, linked);
            await Task.WhenAny(sending, receiving).ConfigureAwait(false);
            await linked.CancelAsync().ConfigureAwait(false);
            await CloseAsync(socket, lifetime.IsCancellationRequested ? "reconnect" : "closing").ConfigureAwait(false);
            await Task.WhenAll(Swallow(sending), Swallow(receiving)).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is WebSocketException or OperationCanceledException)
        {
            _context.Logger.Debug($"Realtime WebSocket {_connection.Id} ended: {ex.Message}");
        }
        finally
        {
            _hub.Disconnect(_connection);
            _connection.Dispose();
            socket?.Dispose();
        }
    }

    private async Task SendLoopAsync(WebSocket socket, SemaphoreSlim send, CancellationToken cancellationToken)
    {
        foreach (var envelope in _hub.Replay(_connection, _lastEventId))
            await SendAsync(socket, send, Encoding.UTF8.GetBytes(envelope.Json), cancellationToken).ConfigureAwait(false);
        await foreach (var envelope in _connection.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
            await SendAsync(socket, send, Encoding.UTF8.GetBytes(envelope.Json), cancellationToken).ConfigureAwait(false);
    }

    private async Task ReceiveLoopAsync(WebSocket socket, SemaphoreSlim send, IWebRealtimeReceiver? receiver, CancellationTokenSource linked)
    {
        var buffer = new byte[MaxMessageBytes];
        // A token bucket per socket: the HTTP rate limits do not see messages on an open socket.
        double tokens = _messagesPerMinute;
        var refilled = _time.GetTimestamp();
        while (socket.State == WebSocketState.Open && !linked.IsCancellationRequested)
        {
            var length = 0;
            WebSocketReceiveResult result;
            do
            {
                if (length == buffer.Length)
                {
                    await socket.CloseOutputAsync(WebSocketCloseStatus.MessageTooBig, "message too large", CancellationToken.None).ConfigureAwait(false);
                    return;
                }
                var received = await socket.ReceiveAsync(buffer.AsMemory(length), linked.Token).ConfigureAwait(false);
                result = new WebSocketReceiveResult(received.Count, received.MessageType, received.EndOfMessage);
                length += received.Count;
            }
            while (!result.EndOfMessage);

            if (result.MessageType == WebSocketMessageType.Close)
                return;
            var now = _time.GetTimestamp();
            tokens = Math.Min(_messagesPerMinute, tokens + _time.GetElapsedTime(refilled, now).TotalMinutes * _messagesPerMinute);
            refilled = now;
            if (tokens < 1)
            {
                _context.Logger.Info($"Realtime WebSocket {_connection.Id} closed: more than {_messagesPerMinute} messages per minute.");
                await socket.CloseOutputAsync(WebSocketCloseStatus.PolicyViolation, "too many messages", CancellationToken.None).ConfigureAwait(false);
                return;
            }
            tokens--;
            if (result.MessageType != WebSocketMessageType.Text)
                continue;
            await HandleAsync(socket, send, receiver, buffer.AsMemory(0, length), linked.Token).ConfigureAwait(false);
        }
    }

    private async Task HandleAsync(WebSocket socket, SemaphoreSlim send, IWebRealtimeReceiver? receiver, ReadOnlyMemory<byte> data, CancellationToken cancellationToken)
    {
        JsonElement message;
        try
        {
            using var document = JsonDocument.Parse(data, new JsonDocumentOptions { MaxDepth = 16 });
            message = document.RootElement.Clone();
        }
        catch (JsonException)
        {
            return;
        }
        if (message.ValueKind == JsonValueKind.Object && message.TryGetProperty("type", out var type) && type.ValueKind == JsonValueKind.String && type.GetString() == "ping")
        {
            await SendAsync(socket, send, Pong, cancellationToken).ConfigureAwait(false);
            return;
        }
        if (receiver is null)
            return;
        try
        {
            await receiver.ReceiveAsync(_connection, message, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _context.Logger.Error($"Realtime receiver failed: {ex.Message}", ex);
        }
    }

    private static async Task SendAsync(WebSocket socket, SemaphoreSlim send, ReadOnlyMemory<byte> data, CancellationToken cancellationToken)
    {
        await send.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await socket.SendAsync(data, WebSocketMessageType.Text, true, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            send.Release();
        }
    }

    private static async Task CloseAsync(WebSocket socket, string reason)
    {
        if (socket.State is not (WebSocketState.Open or WebSocketState.CloseReceived))
            return;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        try
        {
            await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, reason, timeout.Token).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is WebSocketException or OperationCanceledException)
        {
        }
    }

    private static async Task Swallow(Task task)
    {
        try
        {
            await task.ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is WebSocketException or OperationCanceledException or ObjectDisposedException)
        {
        }
    }
}
