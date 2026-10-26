using System.Security.Cryptography;
using System.Text.Json;
using CL.WebLogic.Configuration.Services;
using CL.WebLogic.Realtime.Models;
using Microsoft.AspNetCore.DataProtection;

namespace CL.WebLogic.Realtime.Services;

/// <summary>
/// Short-lived, protected connection tickets for clients that cannot send the session cookie
/// (another origin, native apps): <c>?ticket=…</c> on the SSE or WebSocket URL.
/// </summary>
public sealed class WebRealtimeTickets
{
    private readonly IDataProtector _protector;
    private readonly WebLogicConfigHolder _config;
    private readonly TimeProvider _time;

    /// <summary>Creates the service.</summary>
    public WebRealtimeTickets(IDataProtectionProvider protection, WebLogicConfigHolder config, TimeProvider? time = null)
    {
        _protector = (protection ?? throw new ArgumentNullException(nameof(protection))).CreateProtector("CL.WebLogic.Realtime.Ticket.v1");
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _time = time ?? TimeProvider.System;
    }

    /// <summary>Issues a ticket for <paramref name="identity"/>; returns it with its expiry.</summary>
    public (string Ticket, DateTimeOffset ExpiresAt) Issue(WebRealtimeIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(identity);
        var expires = _time.GetUtcNow().AddSeconds(_config.Current.Realtime.TicketSeconds);
        var payload = new Payload(identity.UserId, identity.TenantId, [.. identity.Groups], [.. identity.Permissions], identity.IsInternal, expires.ToUnixTimeSeconds(), identity.SessionId);
        return (_protector.Protect(JsonSerializer.Serialize(payload)), expires);
    }

    /// <summary>The identity in a valid, unexpired ticket; otherwise null.</summary>
    public WebRealtimeIdentity? Read(string? ticket)
    {
        if (string.IsNullOrEmpty(ticket) || ticket.Length > 8192)
            return null;
        try
        {
            var payload = JsonSerializer.Deserialize<Payload>(_protector.Unprotect(ticket));
            if (payload is null || payload.ExpiresAt < _time.GetUtcNow().ToUnixTimeSeconds())
                return null;
            return new WebRealtimeIdentity
            {
                UserId = payload.UserId,
                SessionId = payload.SessionId,
                TenantId = payload.TenantId,
                Groups = payload.Groups ?? [],
                Permissions = payload.Permissions ?? [],
                IsInternal = payload.IsInternal
            };
        }
        catch (Exception ex) when (ex is CryptographicException or JsonException)
        {
            return null;
        }
    }

    private sealed record Payload(string? UserId, string? TenantId, string[]? Groups, string[]? Permissions, bool IsInternal, long ExpiresAt, string? SessionId = null);
}
