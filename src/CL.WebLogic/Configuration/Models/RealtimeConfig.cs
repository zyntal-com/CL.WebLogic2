using CodeLogic.Core.Configuration;

namespace CL.WebLogic.Configuration.Models;

/// <summary>The realtime hub (server-sent events and WebSockets).</summary>
public sealed class RealtimeConfig
{
    /// <summary>Serve the realtime endpoints.</summary>
    [ConfigField(Label = "Enabled", Group = "Realtime", Order = 1)]
    public bool Enabled { get; set; } = true;

    /// <summary>Allow anonymous connections (they only receive Public messages).</summary>
    [ConfigField(Label = "Allow Anonymous", Group = "Realtime", Order = 2)]
    public bool AllowAnonymous { get; set; } = true;

    /// <summary>Serve the WebSocket transport (SSE is always available while enabled).</summary>
    [ConfigField(Label = "WebSockets", Group = "Realtime", Order = 3)]
    public bool WebSockets { get; set; } = true;

    /// <summary>Messages kept for Last-Event-ID replay.</summary>
    [ConfigField(Label = "Replay Size", Min = 0, Max = 10000, Group = "Realtime", Order = 4, Collapsed = true)]
    public int ReplaySize { get; set; } = 200;

    /// <summary>How long messages stay replayable.</summary>
    [ConfigField(Label = "Replay Minutes", Min = 0, Max = 1440, Group = "Realtime", Order = 5, Collapsed = true)]
    public int ReplayMinutes { get; set; } = 5;

    /// <summary>Keep-alive interval.</summary>
    [ConfigField(Label = "Heartbeat Seconds", Min = 5, Max = 300, Group = "Realtime", Order = 6, Collapsed = true)]
    public int HeartbeatSeconds { get; set; } = 20;

    /// <summary>Connections are closed after this long so clients re-authenticate (groups and permissions are re-read).</summary>
    [ConfigField(Label = "Max Connection Minutes", Min = 1, Max = 1440, Group = "Realtime", Order = 7, Collapsed = true)]
    public int MaxConnectionMinutes { get; set; } = 60;

    /// <summary>
    /// How often open connections of session-signed-in users are checked against the session store;
    /// a connection whose session was revoked, evicted or expired is closed (on every node). 0 = off.
    /// </summary>
    [ConfigField(Label = "Session Check Seconds", Min = 0, Max = 3600, Group = "Realtime", Order = 17, Collapsed = true)]
    public int SessionCheckSeconds { get; set; } = 30;

    /// <summary>Maximum open connections on this node.</summary>
    [ConfigField(Label = "Max Connections", Min = 1, Max = 1000000, Group = "Realtime", Order = 8, Collapsed = true)]
    public int MaxConnections { get; set; } = 10000;

    /// <summary>Maximum open connections per client IP.</summary>
    [ConfigField(Label = "Max Connections / Client", Min = 1, Max = 1000, Group = "Realtime", Order = 9, Collapsed = true)]
    public int MaxConnectionsPerClient { get; set; } = 20;

    /// <summary>Messages queued per connection before the oldest are dropped (slow clients).</summary>
    [ConfigField(Label = "Queue Size", Min = 8, Max = 10000, Group = "Realtime", Order = 10, Collapsed = true)]
    public int QueueSize { get; set; } = 256;

    /// <summary>Origins (besides this site) allowed to open WebSockets, e.g. <c>https://app.example.com</c>.</summary>
    [ConfigField(Label = "Allowed Origins", Group = "Realtime", Order = 11, Collapsed = true)]
    public List<string> AllowedOrigins { get; set; } = [];

    /// <summary>Lifetime of connection tickets (cross-origin clients without cookies).</summary>
    [ConfigField(Label = "Ticket Seconds", Min = 10, Max = 3600, Group = "Realtime", Order = 12, Collapsed = true)]
    public int TicketSeconds { get; set; } = 60;

    /// <summary>Publish a summary of every request (method, path, status, duration — never IPs) to the Internal audience.</summary>
    [ConfigField(Label = "Broadcast Requests", Group = "Realtime", Order = 13, Collapsed = true)]
    public bool BroadcastRequests { get; set; }

    /// <summary>Transport the client uses: <c>sse</c> or <c>ws</c>.</summary>
    [ConfigField(Label = "Client Transport", Description = "sse or ws", Group = "Realtime", Order = 15, Collapsed = true)]
    public string ClientTransport { get; set; } = "sse";

    /// <summary>The client connects on page load.</summary>
    [ConfigField(Label = "Client Auto-Connect", Group = "Realtime", Order = 16)]
    public bool ClientAutoConnect { get; set; }

    /// <summary>Permission that admits a connection to the Internal audience (and to config change notices).</summary>
    [ConfigField(Label = "Internal Permission", Group = "Realtime", Order = 14, Collapsed = true)]
    public string InternalPermission { get; set; } = "weblogic.internal";
}
