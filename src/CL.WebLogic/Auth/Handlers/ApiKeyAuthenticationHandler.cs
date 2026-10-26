using System.Security.Cryptography;
using System.Text;
using CL.WebLogic.Auth.ApiKeys;
using CL.WebLogic.Configuration.Services;
using CL.WebLogic.Security.Identity;
using CL.WebLogic.Security.Pipeline;
using CL.WebLogic.Security.Services;

namespace CL.WebLogic.Auth.Handlers;

/// <summary>
/// Authenticates <c>X-Api-Key: wl_…</c>, <c>Authorization: ApiKey wl_…</c> or
/// <c>Authorization: Bearer wl_…</c>. A present but invalid key fails the request (401); it does
/// not fall back to other credentials. A key owned by a user stops working when that user is
/// deleted or disabled.
/// </summary>
public sealed class ApiKeyAuthenticationHandler : IWebAuthenticationHandler
{
    private readonly IWebApiKeyStore _store;
    private readonly WebLogicConfigHolder _config;
    private readonly TimeProvider _time;
    private readonly Users.IWebUserStore? _users;

    /// <summary>Creates the handler.</summary>
    public ApiKeyAuthenticationHandler(IWebApiKeyStore store, WebLogicConfigHolder config, TimeProvider? time = null, Users.IWebUserStore? users = null)
    {
        _users = users is Users.NullWebUserStore ? null : users;
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _time = time ?? TimeProvider.System;
    }

    /// <inheritdoc/>
    public string Scheme => WebAuthenticationSchemes.ApiKey;

    /// <inheritdoc/>
    public int Order => 200;

    /// <inheritdoc/>
    public async ValueTask<WebAuthenticationResult> AuthenticateAsync(WebSecurityContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var settings = _config.Current.Auth.ApiKeys;
        if (!settings.Enabled)
            return WebAuthenticationResult.NoResult;

        var presented = ReadKey(context, settings.HeaderName);
        if (presented is null)
            return WebAuthenticationResult.NoResult;
        if (!WebApiKeyFormat.TryParse(presented, out var prefix))
            return WebAuthenticationResult.Fail("api-key-malformed");

        var key = await _store.FindByPrefixAsync(prefix, cancellationToken).ConfigureAwait(false);
        var expected = Encoding.ASCII.GetBytes(key?.SecretHash ?? new string('0', 64));
        var actual = Encoding.ASCII.GetBytes(WebApiKeyFormat.Hash(presented));
        if (key is null || !CryptographicOperations.FixedTimeEquals(expected, actual))
            return WebAuthenticationResult.Fail("api-key-unknown");

        var now = _time.GetUtcNow();
        if (key.RevokedAt is not null || key.ExpiresAt <= now)
            return WebAuthenticationResult.Fail("api-key-expired");
        if (key.AllowedNetworks.Count > 0
            && (context.ClientIp is null || !IpNetworkParser.ContainsAny(IpNetworkParser.ParseAll(key.AllowedNetworks), context.ClientIp)))
            return WebAuthenticationResult.Fail("api-key-network");
        if (context.TenantId is not null && key.TenantId is not null && !string.Equals(context.TenantId, key.TenantId, StringComparison.Ordinal))
            return WebAuthenticationResult.Fail("api-key-tenant");

        if (key.UserId is not null && _users is not null)
        {
            var owner = await _users.FindByIdAsync(key.UserId, cancellationToken).ConfigureAwait(false);
            if (owner is null || owner.IsDisabled || Users.WebSuspension.Of(owner, now) is not null)
                return WebAuthenticationResult.Fail("api-key-owner");
        }

        if (key.LastUsedAt is null || now - key.LastUsedAt.Value >= TimeSpan.FromMinutes(settings.TouchIntervalMinutes))
            await _store.UpdateAsync(key with { LastUsedAt = now }, cancellationToken).ConfigureAwait(false);

        var claims = new Dictionary<string, string>(StringComparer.Ordinal) { [WebIdentityClaims.KeyId] = key.Id };
        if (!string.IsNullOrEmpty(key.RateLimitPolicy))
            claims[WebIdentityClaims.RateLimitPolicy] = key.RateLimitPolicy;

        return WebAuthenticationResult.Success(new WebIdentity
        {
            UserId = key.UserId ?? "apikey:" + key.Id,
            UserName = key.Name,
            Scheme = WebAuthenticationSchemes.ApiKey,
            IsAmbient = false,
            Groups = key.Groups,
            Permissions = key.Permissions,
            Scopes = key.Scopes,
            TenantId = key.TenantId ?? context.TenantId,
            AuthenticationMethods = ["apikey"],
            Claims = claims
        });
    }

    private static string? ReadKey(WebSecurityContext context, string headerName)
    {
        var headers = context.HttpContext.Request.Headers;
        var header = headers[headerName].ToString();
        if (header.Length > 0)
            return header.Trim();

        var authorization = headers.Authorization.ToString();
        foreach (var scheme in (string[])["Bearer ", "ApiKey "])
        {
            if (authorization.StartsWith(scheme, StringComparison.OrdinalIgnoreCase))
            {
                var value = authorization[scheme.Length..].Trim();
                return WebApiKeyFormat.LooksLikeKey(value) ? value : null;
            }
        }
        return null;
    }
}
