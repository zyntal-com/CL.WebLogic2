using System.Text.Json;
using CL.WebLogic.Realtime.Models;

namespace CL.WebLogic.Realtime.Services;

/// <summary>
/// Handles messages clients send over the WebSocket transport (other than <c>ping</c>). Optional;
/// without one, client messages are ignored. Treat the payload as untrusted.
/// </summary>
public interface IWebRealtimeReceiver
{
    /// <summary>Handles one message (at most 16 KB of JSON).</summary>
    ValueTask ReceiveAsync(WebRealtimeConnection connection, JsonElement message, CancellationToken cancellationToken);
}
