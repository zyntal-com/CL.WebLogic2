using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CL.WebLogic.Auth.Users;
using CL.WebLogic.Hosting.Services;
using CodeLogic.Core.Results;
using Microsoft.AspNetCore.DataProtection;

namespace CL.WebLogic.Auth.Tokens;

/// <summary>Default <see cref="IWebSecurityTokenService"/> on ASP.NET DataProtection.</summary>
public sealed class WebSecurityTokenService : IWebSecurityTokenService
{
    private const string ProtectorPurpose = "CL.WebLogic.Auth.Tokens.v1";

    private readonly IDataProtector _root;
    private readonly IWebUserStore _users;
    private readonly IWebOneTimeUseStore _oneTime;
    private readonly IWebLogicContext _context;
    private readonly TimeProvider _time;

    /// <summary>Creates the service.</summary>
    public WebSecurityTokenService(IDataProtectionProvider protection, IWebUserStore users, IWebOneTimeUseStore oneTime, IWebLogicContext context, TimeProvider? time = null)
    {
        ArgumentNullException.ThrowIfNull(protection);
        _root = protection.CreateProtector(ProtectorPurpose);
        _users = users ?? throw new ArgumentNullException(nameof(users));
        _oneTime = oneTime ?? throw new ArgumentNullException(nameof(oneTime));
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _time = time ?? TimeProvider.System;
    }

    /// <inheritdoc/>
    public string Create(string purpose, WebUser user, TimeSpan lifetime, string? data = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(purpose);
        ArgumentNullException.ThrowIfNull(user);
        var payload = new TokenPayload
        {
            UserId = user.Id,
            Stamp = user.SecurityStamp,
            ExpiresAt = _time.GetUtcNow().Add(lifetime).ToUnixTimeSeconds(),
            Data = data,
            Nonce = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(8))
        };
        return _root.CreateProtector(purpose).Protect(JsonSerializer.Serialize(payload));
    }

    /// <inheritdoc/>
    public async Task<Result<WebSecurityToken>> ValidateAsync(string purpose, string token, bool consume, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(purpose);
        if (string.IsNullOrWhiteSpace(token) || token.Length > 4096)
            return Invalid("empty or oversized");

        TokenPayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<TokenPayload>(_root.CreateProtector(purpose).Unprotect(token.Trim()));
        }
        catch (Exception ex) when (ex is CryptographicException or JsonException or FormatException)
        {
            return Invalid("unreadable");
        }
        if (payload?.UserId is null)
            return Invalid("unreadable");

        var expiresAt = DateTimeOffset.FromUnixTimeSeconds(payload.ExpiresAt);
        if (expiresAt <= _time.GetUtcNow())
            return Invalid("expired");

        var user = await _users.FindByIdAsync(payload.UserId, cancellationToken).ConfigureAwait(false);
        if (user is null || !string.Equals(user.SecurityStamp, payload.Stamp, StringComparison.Ordinal))
            return Invalid("user changed");

        if (consume)
        {
            var key = "token:" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(purpose + "|" + token.Trim())));
            if (!await _oneTime.TryUseAsync(key, expiresAt, cancellationToken).ConfigureAwait(false))
                return Invalid("already used");
        }

        return Result<WebSecurityToken>.Success(new WebSecurityToken { Purpose = purpose, User = user, Data = payload.Data, ExpiresAt = expiresAt });
    }

    private Result<WebSecurityToken> Invalid(string reason)
    {
        _context.Logger.Debug($"Account token rejected: {reason}");
        return Result<WebSecurityToken>.Failure(Error.Validation("weblogic.auth.token_invalid", "The link is invalid or has expired."));
    }

    private sealed class TokenPayload
    {
        public string? UserId { get; set; }

        public string? Stamp { get; set; }

        public long ExpiresAt { get; set; }

        public string? Data { get; set; }

        public string? Nonce { get; set; }
    }
}
