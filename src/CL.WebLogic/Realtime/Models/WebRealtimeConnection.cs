using System.Threading.Channels;
using CL.WebLogic.Security.Identity;

namespace CL.WebLogic.Realtime.Models;

/// <summary>An open realtime connection: who it is, what it subscribed to, and its bounded send queue.</summary>
public sealed class WebRealtimeConnection : IDisposable
{
    private readonly Channel<WebRealtimeEnvelope> _queue;
    private readonly CancellationTokenSource _closed = new();
    private long _dropped;

    internal WebRealtimeConnection(string id, WebRealtimeIdentity identity, IReadOnlySet<string> channels, WebRealtimeTransport transport, string? clientIp, int queueSize, DateTimeOffset openedAt)
    {
        Id = id;
        Identity = identity;
        Channels = channels;
        Transport = transport;
        ClientIp = clientIp;
        OpenedAt = openedAt;
        _queue = Channel.CreateBounded<WebRealtimeEnvelope>(new BoundedChannelOptions(queueSize)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        }, _ => Interlocked.Increment(ref _dropped));
    }

    /// <summary>Connection id.</summary>
    public string Id { get; }

    /// <summary>What the connection may receive.</summary>
    public WebRealtimeIdentity Identity { get; }

    /// <summary>Subscribed channels (empty = all channels).</summary>
    public IReadOnlySet<string> Channels { get; }

    /// <summary>Transport.</summary>
    public WebRealtimeTransport Transport { get; }

    /// <summary>Client IP (for per-client limits only; never published).</summary>
    public string? ClientIp { get; }

    /// <summary>When it opened.</summary>
    public DateTimeOffset OpenedAt { get; }

    /// <summary>Messages dropped because the client was too slow.</summary>
    public long Dropped => Interlocked.Read(ref _dropped);

    /// <summary>Cancelled when the server closes the connection (sign-out, shutdown, lifetime).</summary>
    public CancellationToken Closed => _closed.Token;

    internal ChannelReader<WebRealtimeEnvelope> Reader => _queue.Reader;

    /// <summary>True when the connection may receive <paramref name="envelope"/>.</summary>
    public bool Accepts(WebRealtimeEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        if (envelope.Channel is not null && Channels.Count > 0 && !Channels.Contains(envelope.Channel))
            return false;
        if (envelope.TenantId is not null && !string.Equals(Identity.TenantId, envelope.TenantId, StringComparison.Ordinal))
            return false;
        var audience = envelope.Audience;
        return audience.Kind switch
        {
            WebRealtimeAudienceKind.Public => true,
            WebRealtimeAudienceKind.Authenticated => Identity.IsAuthenticated,
            WebRealtimeAudienceKind.Group => Identity.Groups.Contains(audience.Value!, StringComparer.OrdinalIgnoreCase),
            WebRealtimeAudienceKind.Permission => Identity.IsAuthenticated && WebPermissionMatcher.Grants(Identity.Permissions, audience.Value!),
            WebRealtimeAudienceKind.User => Identity.UserId is not null && string.Equals(Identity.UserId, audience.Value, StringComparison.Ordinal),
            WebRealtimeAudienceKind.Tenant => Identity.IsAuthenticated && string.Equals(Identity.TenantId, audience.Value, StringComparison.Ordinal),
            WebRealtimeAudienceKind.Internal => Identity.IsInternal,
            _ => false
        };
    }

    internal bool TryEnqueue(WebRealtimeEnvelope envelope) => !_closed.IsCancellationRequested && _queue.Writer.TryWrite(envelope);

    /// <summary>Closes the connection and releases it (called by the transport that owns it).</summary>
    public void Dispose()
    {
        Close();
        _closed.Dispose();
    }

    internal void Close()
    {
        _queue.Writer.TryComplete();
        try
        {
            _closed.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }
    }
}
