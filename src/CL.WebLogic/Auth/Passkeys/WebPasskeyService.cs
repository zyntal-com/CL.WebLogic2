using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CL.WebLogic.Auth.Events;
using CL.WebLogic.Auth.Notifications;
using CL.WebLogic.Auth.Tokens;
using CL.WebLogic.Auth.Users;
using CL.WebLogic.Configuration.Models;
using CL.WebLogic.Configuration.Services;
using CL.WebLogic.Hosting.Services;
using CL.WebLogic.Security.Audit;
using CL.WebLogic.Security.Pipeline;
using CodeLogic.Core.Results;
using Microsoft.AspNetCore.DataProtection;

namespace CL.WebLogic.Auth.Passkeys;

/// <summary>
/// WebAuthn ceremonies: registering passkeys and verifying assertions (signature, challenge, origin,
/// RP id, flags, counter), plus the user's passkey list. Ceremony state travels as a protected token
/// and each challenge is accepted once, so ceremonies work across nodes. Sign-in itself goes through
/// <c>request.Auth</c>, which applies the account rules.
/// </summary>
public sealed partial class WebPasskeyService
{
    private const string StatePurpose = "CL.WebLogic.Auth.Passkeys.State.v1";
    private const int MaxNameLength = 64;

    private readonly IWebPasskeyStore _store;
    private readonly IWebUserStore _users;
    private readonly IWebOneTimeUseStore _oneTime;
    private readonly WebNotificationDispatcher _notifications;
    private readonly IWebAuditLog _audit;
    private readonly IDataProtector _state;
    private readonly WebLogicConfigHolder _config;
    private readonly IWebLogicContext _context;
    private readonly TimeProvider _time;

    /// <summary>Creates the service.</summary>
    public WebPasskeyService(IWebPasskeyStore store, IWebUserStore users, IWebOneTimeUseStore oneTime, WebNotificationDispatcher notifications,
        IWebAuditLog audit, IDataProtectionProvider protection, WebLogicConfigHolder config, IWebLogicContext context, TimeProvider? time = null)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _users = users ?? throw new ArgumentNullException(nameof(users));
        _oneTime = oneTime ?? throw new ArgumentNullException(nameof(oneTime));
        _notifications = notifications ?? throw new ArgumentNullException(nameof(notifications));
        _audit = audit ?? throw new ArgumentNullException(nameof(audit));
        _state = (protection ?? throw new ArgumentNullException(nameof(protection))).CreateProtector(StatePurpose);
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _time = time ?? TimeProvider.System;
    }

    private PasskeyConfig Settings => _config.Current.Auth.Passkeys;

    /// <summary>The user's passkeys.</summary>
    public Task<IReadOnlyList<WebPasskey>> ListAsync(WebUser user, CancellationToken cancellationToken) =>
        _store.ListAsync((user ?? throw new ArgumentNullException(nameof(user))).Id, cancellationToken);

    /// <summary>Renames one of the user's passkeys.</summary>
    public Task<bool> RenameAsync(WebUser user, string credentialId, string name, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        return _store.RenameAsync(user.Id, credentialId ?? "", CleanName(name), cancellationToken);
    }

    /// <summary>Removes one of the user's passkeys; the user is notified.</summary>
    public async Task<bool> DeleteAsync(WebSecurityContext security, WebUser user, string credentialId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        var passkey = await _store.FindAsync(credentialId ?? "", cancellationToken).ConfigureAwait(false);
        if (passkey is null || passkey.UserId != user.Id || !await _store.DeleteAsync(user.Id, passkey.CredentialId, cancellationToken).ConfigureAwait(false))
            return false;
        _context.Events.Publish(new PasskeyChangedEvent(user.Id, passkey.CredentialId, Added: false));
        _notifications.Enqueue(user, WebSecurityNotificationKind.PasskeyRemoved, values: new Dictionary<string, string> { ["name"] = passkey.Name });
        _audit.Record(security, "auth.passkey", "removed", data: new Dictionary<string, string> { ["user"] = user.Id });
        return true;
    }

    /// <summary>The RP id for this request: configured, else the host of PublicBaseUrl, else the request host.</summary>
    internal string RelyingPartyId(WebSecurityContext security)
    {
        if (Settings.RelyingPartyId.Length > 0)
            return Settings.RelyingPartyId.ToLowerInvariant();
        if (Uri.TryCreate(_config.Current.PublicBaseUrl, UriKind.Absolute, out var publicUrl))
            return publicUrl.Host.ToLowerInvariant();
        return security.HttpContext.Request.Host.Host.ToLowerInvariant();
    }

    /// <summary>Origins allowed to run ceremonies: configured, else PublicBaseUrl's, else this request's.</summary>
    private IEnumerable<string> Origins(WebSecurityContext security)
    {
        if (Settings.Origins.Count > 0)
            return Settings.Origins.Select(o => o.TrimEnd('/'));
        if (Uri.TryCreate(_config.Current.PublicBaseUrl, UriKind.Absolute, out var publicUrl))
            return [publicUrl.GetLeftPart(UriPartial.Authority)];
        var request = security.HttpContext.Request;
        return [request.Scheme + "://" + request.Host.Value];
    }

    private string RelyingPartyName() =>
        Settings.RelyingPartyName.Length > 0 ? Settings.RelyingPartyName : _config.Current.SiteName;

    private sealed record CeremonyState(string Kind, string Challenge, string RpId, string? UserId, string? UserHandle, bool RequireVerification, long ExpiresAt);

    private (string Challenge, string Token) NewState(string kind, string rpId, string? userId, string? userHandle, bool requireVerification)
    {
        var challenge = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));
        var expires = _time.GetUtcNow().AddSeconds(Settings.TimeoutSeconds + 60).ToUnixTimeSeconds();
        var state = new CeremonyState(kind, challenge, rpId, userId, userHandle, requireVerification, expires);
        return (challenge, _state.Protect(JsonSerializer.Serialize(state)));
    }

    /// <summary>Opens and spends the state: wrong kind, expired, tampered or already used → null.</summary>
    private async Task<CeremonyState?> SpendStateAsync(string token, string kind, CancellationToken cancellationToken)
    {
        CeremonyState? state;
        try
        {
            state = JsonSerializer.Deserialize<CeremonyState>(_state.Unprotect(token ?? ""));
        }
        catch (Exception ex) when (ex is CryptographicException or JsonException)
        {
            return null;
        }
        if (state is null || state.Kind != kind || state.ExpiresAt < _time.GetUtcNow().ToUnixTimeSeconds())
            return null;
        var spent = await _oneTime.TryUseAsync("passkey:" + state.Challenge, DateTimeOffset.FromUnixTimeSeconds(state.ExpiresAt), cancellationToken).ConfigureAwait(false);
        return spent ? state : null;
    }

    /// <summary>Checks client data against the ceremony; null when it matches, else the reason.</summary>
    private string? CheckClientData(WebClientData? clientData, string type, CeremonyState state, WebSecurityContext security)
    {
        if (clientData is null)
            return "malformed clientDataJSON";
        if (clientData.Type != type)
            return "wrong ceremony type " + clientData.Type;
        if (!CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(clientData.Challenge), Encoding.ASCII.GetBytes(state.Challenge)))
            return "wrong challenge";
        if (clientData.CrossOrigin)
            return "cross-origin ceremony";
        return Origins(security).Contains(clientData.Origin, StringComparer.Ordinal) ? null : "origin " + clientData.Origin + " is not allowed";
    }

    /// <summary>Checks the RP id hash and flags; null when they are acceptable, else the reason.</summary>
    private static string? CheckAuthenticatorData(WebAuthenticatorData data, CeremonyState state)
    {
        if (!CryptographicOperations.FixedTimeEquals(data.RpIdHash, SHA256.HashData(Encoding.UTF8.GetBytes(state.RpId))))
            return "credential is for another RP id";
        if (!data.Has(WebAuthenticatorBits.UserPresent))
            return "user not present";
        if (state.RequireVerification && !data.Has(WebAuthenticatorBits.UserVerified))
            return "user not verified";
        if (data.Has(WebAuthenticatorBits.BackedUp) && !data.Has(WebAuthenticatorBits.BackupEligible))
            return "backed up but not backup eligible";
        return null;
    }

    private static string? Text(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static byte[]? Decode(string? base64Url)
    {
        if (string.IsNullOrEmpty(base64Url) || base64Url.Length > 64 * 1024)
            return null;
        try
        {
            return Base64Url.DecodeFromChars(base64Url);
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private static string CleanName(string? name)
    {
        var cleaned = new string((name ?? "").Where(c => !char.IsControl(c)).ToArray()).Trim();
        return cleaned.Length == 0 ? "Passkey" : cleaned.Length > MaxNameLength ? cleaned[..MaxNameLength] : cleaned;
    }

    private Result<T> Fail<T>(WebSecurityContext security, string ceremony, string reason, string message = "The passkey could not be verified.")
    {
        _context.Logger.Warning($"Passkey {ceremony} refused: {reason}");
        _audit.Record(security, "auth.passkey", ceremony + "-refused", data: new Dictionary<string, string> { ["reason"] = reason });
        return Result<T>.Failure(Error.Validation("weblogic.passkeys.failed", message));
    }
}
