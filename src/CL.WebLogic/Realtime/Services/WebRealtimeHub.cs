using System.Collections.Concurrent;
using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using CL.WebLogic.Configuration.Services;
using CL.WebLogic.Hosting.Services;
using CL.WebLogic.Realtime.Models;
using CL.WebLogic.Runtime.Services;
using CL.WebLogic.Security.RateLimiting;

namespace CL.WebLogic.Realtime.Services;

/// <summary>
/// Publishes realtime messages and fans them out to the connections allowed to receive them.
/// Messages go through the backplane, so every node delivers to its own connections; the replay
/// buffer only ever hands a connection messages its audience allows.
/// </summary>
public sealed partial class WebRealtimeHub : IDisposable
{
    /// <summary>Type of the message sent when a resume point is no longer buffered.</summary>
    public const string ReplayGapType = "weblogic.replay-gap";

    private readonly IWebRealtimeBackplane _backplane;
    private readonly WebLogicConfigHolder _config;
    private readonly IWebLogicContext _context;
    private readonly TimeProvider _time;
    private readonly IDisposable _subscription;
    private readonly WebRealtimeReplayBuffer _replay = new();
    private readonly ConcurrentDictionary<string, WebRealtimeConnection> _connections = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, int> _perClient = new(StringComparer.Ordinal);
    private readonly string _node = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(4));
    private long _sequence;

    /// <summary>Creates the hub.</summary>
    public WebRealtimeHub(IWebRealtimeBackplane backplane, WebLogicConfigHolder config, IWebLogicContext context, TimeProvider? time = null)
    {
        _backplane = backplane ?? throw new ArgumentNullException(nameof(backplane));
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _time = time ?? TimeProvider.System;
        _subscription = _backplane.Subscribe(DeliverAsync);
    }

    /// <summary>Open connections on this node.</summary>
    public int ConnectionCount => _connections.Count;

    /// <summary>Buffered replay messages.</summary>
    public int ReplayCount => _replay.Count;

    /// <summary>Open connections on this node.</summary>
    public IReadOnlyCollection<WebRealtimeConnection> Connections => [.. _connections.Values];

    /// <summary>
    /// Publishes a message to every tenant. <paramref name="type"/> and <paramref name="channel"/> match
    /// <c>^[\w.:-]{1,100}$</c>; <paramref name="data"/> is serialized with the default JSON options.
    /// Multi-tenant apps publish tenant data with <see cref="PublishToTenantAsync"/>.
    /// </summary>
    public Task PublishAsync(string type, object? data, WebRealtimeAudience audience, string? channel = null, CancellationToken cancellationToken = default) =>
        PublishCoreAsync(null, type, data, audience, channel, cancellationToken);

    /// <summary>
    /// Publishes a message that only connections of <paramref name="tenantId"/> receive (and
    /// replay), whatever the audience — e.g. <c>Group("admins")</c> means that tenant's admins.
    /// </summary>
    public Task PublishToTenantAsync(string tenantId, string type, object? data, WebRealtimeAudience audience, string? channel = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(tenantId);
        return PublishCoreAsync(tenantId, type, data, audience, channel, cancellationToken);
    }

    private Task PublishCoreAsync(string? tenantId, string type, object? data, WebRealtimeAudience audience, string? channel, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(audience);
        if (!IsValidName(type))
            throw new ArgumentException($"Realtime type '{type}' must match ^[\\w.:-]{{1,100}}$.", nameof(type));
        if (channel is not null && !IsValidName(channel))
            throw new ArgumentException($"Realtime channel '{channel}' must match ^[\\w.:-]{{1,100}}$.", nameof(channel));

        var id = _node + "-" + Interlocked.Increment(ref _sequence).ToString("x", System.Globalization.CultureInfo.InvariantCulture);
        var now = _time.GetUtcNow();
        var json = JsonSerializer.Serialize(new Frame(id, type, channel, data, now.ToString("O", System.Globalization.CultureInfo.InvariantCulture)), WebJson.Default);
        return _backplane.PublishAsync(new WebRealtimeEnvelope(id, type, channel, audience, json, now, tenantId), cancellationToken);
    }

    /// <summary>
    /// Opens a connection, or returns null with a reason when a limit is reached.
    /// Close it with <see cref="Disconnect"/>.
    /// </summary>
    public WebRealtimeConnection? Connect(WebRealtimeIdentity identity, IReadOnlySet<string> channels, WebRealtimeTransport transport, string? clientIp, out string? rejection)
    {
        ArgumentNullException.ThrowIfNull(identity);
        var config = _config.Current.Realtime;
        rejection = null;
        if (_connections.Count >= config.MaxConnections)
        {
            rejection = "max-connections";
            return null;
        }
        // Counted like the rate limits: IPv6 per network prefix, or one host could rotate addresses.
        var clientKey = IPAddress.TryParse(clientIp, out var address)
            ? ClientAddressKey.For(address, _config.Current.Security.RateLimits.Ipv6PrefixLength)
            : clientIp ?? "unknown";
        var count = _perClient.AddOrUpdate(clientKey, 1, (_, current) => current + 1);
        if (count > config.MaxConnectionsPerClient)
        {
            Release(clientKey);
            rejection = "max-connections-per-client";
            return null;
        }
        var connection = new WebRealtimeConnection(Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(8)), identity, channels, transport, clientKey, config.QueueSize, _time.GetUtcNow());
        _connections[connection.Id] = connection;
        return connection;
    }

    /// <summary>Removes and closes a connection.</summary>
    public void Disconnect(WebRealtimeConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);
        if (_connections.TryRemove(connection.Id, out _))
            Release(connection.ClientIp ?? "unknown");
        connection.Close();
    }

    /// <summary>Closes every connection of <paramref name="userId"/> (sign-out, password change); clients reconnect with their new identity.</summary>
    public int CloseUser(string userId)
    {
        var closed = 0;
        foreach (var connection in _connections.Values.Where(c => string.Equals(c.Identity.UserId, userId, StringComparison.Ordinal)))
        {
            Disconnect(connection);
            closed++;
        }
        return closed;
    }

    /// <summary>Closes the connections opened with session <paramref name="sessionIdHash"/> (the session was revoked or expired).</summary>
    public int CloseSession(string sessionIdHash)
    {
        var closed = 0;
        foreach (var connection in _connections.Values.Where(c => string.Equals(c.Identity.SessionId, sessionIdHash, StringComparison.Ordinal)))
        {
            Disconnect(connection);
            closed++;
        }
        return closed;
    }

    /// <summary>Closes every connection opened with the API key <paramref name="keyId"/> (revocation).</summary>
    public int CloseApiKey(string keyId)
    {
        var closed = 0;
        foreach (var connection in _connections.Values.Where(c => string.Equals(c.Identity.ApiKeyId, keyId, StringComparison.Ordinal)))
        {
            Disconnect(connection);
            closed++;
        }
        return closed;
    }

    /// <summary>Closes every connection (shutdown); clients reconnect elsewhere or later.</summary>
    public void CloseAll()
    {
        foreach (var connection in _connections.Values)
            Disconnect(connection);
    }

    /// <summary>Messages to send a resuming connection: those after <paramref name="lastEventId"/> it may receive, or a resync notice.</summary>
    public IReadOnlyList<WebRealtimeEnvelope> Replay(WebRealtimeConnection connection, string? lastEventId)
    {
        ArgumentNullException.ThrowIfNull(connection);
        if (string.IsNullOrEmpty(lastEventId))
            return [];
        var (found, messages) = _replay.After(lastEventId);
        if (!found)
        {
            var now = _time.GetUtcNow();
            var json = JsonSerializer.Serialize(new Frame(null, ReplayGapType, null, null, now.ToString("O", System.Globalization.CultureInfo.InvariantCulture)), WebJson.Default);
            return [new WebRealtimeEnvelope("", ReplayGapType, null, WebRealtimeAudience.Public, json, now)];
        }
        return [.. messages.Where(connection.Accepts)];
    }

    /// <summary>True for a valid type or channel name.</summary>
    public static bool IsValidName(string? value) => value is not null && Name().IsMatch(value);

    /// <inheritdoc/>
    public void Dispose()
    {
        _subscription.Dispose();
        CloseAll();
    }

    private ValueTask DeliverAsync(WebRealtimeEnvelope envelope)
    {
        var config = _config.Current.Realtime;
        _replay.Add(envelope, config.ReplaySize, TimeSpan.FromMinutes(config.ReplayMinutes), _time.GetUtcNow());
        foreach (var connection in _connections.Values)
        {
            if (connection.Accepts(envelope) && !connection.TryEnqueue(envelope))
                _context.Logger.Debug($"Realtime connection {connection.Id} is closed; message {envelope.Id} not queued.");
        }
        return ValueTask.CompletedTask;
    }

    private void Release(string clientKey)
    {
        while (_perClient.TryGetValue(clientKey, out var current))
        {
            if (current <= 1 ? _perClient.TryRemove(new KeyValuePair<string, int>(clientKey, current)) : _perClient.TryUpdate(clientKey, current - 1, current))
                return;
        }
    }

    [GeneratedRegex(@"^[\w.:-]{1,100}$", RegexOptions.CultureInvariant, 1000)]
    private static partial Regex Name();

    private sealed record Frame(string? Id, string Type, string? Channel, object? Data, string Timestamp);
}
