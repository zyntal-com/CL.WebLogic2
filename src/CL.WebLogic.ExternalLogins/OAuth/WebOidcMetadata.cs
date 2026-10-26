using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Text.Json;
using CL.WebLogic.ExternalLogins.Jwt;

namespace CL.WebLogic.ExternalLogins.OAuth;

/// <summary>
/// Discovery documents and signing keys of OpenID Connect issuers, cached for a day. An ID token
/// signed with an unknown key id refreshes the key set (at most every 30 seconds per issuer), so
/// issuers can rotate keys without a restart and forged key ids cannot cause a fetch storm.
/// </summary>
public sealed class WebOidcMetadata
{
    private static readonly TimeSpan CacheTime = TimeSpan.FromHours(24);
    private static readonly TimeSpan MinRefresh = TimeSpan.FromSeconds(30);
    private const long MaxDocumentBytes = 512 * 1024;

    private readonly IHttpClientFactory _http;
    private readonly TimeProvider _time;
    private readonly ConcurrentDictionary<string, (WebOidcConfiguration Value, DateTimeOffset Until)> _configurations = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, (IReadOnlyList<WebJsonWebKey> Keys, DateTimeOffset LoadedAt)> _keys = new(StringComparer.Ordinal);

    /// <summary>Creates the cache.</summary>
    public WebOidcMetadata(IHttpClientFactory http, TimeProvider? time = null)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _time = time ?? TimeProvider.System;
    }

    /// <summary>The discovery document of <paramref name="authority"/> (its <c>issuer</c> must match).</summary>
    public async Task<WebOidcConfiguration> GetConfigurationAsync(string authority, CancellationToken cancellationToken)
    {
        var now = _time.GetUtcNow();
        if (_configurations.TryGetValue(authority, out var cached) && cached.Until > now)
            return cached.Value;

        using var document = await GetJsonAsync(authority.TrimEnd('/') + "/.well-known/openid-configuration", cancellationToken).ConfigureAwait(false);
        var root = document.RootElement;
        var configuration = new WebOidcConfiguration(
            Required(root, "issuer"), Required(root, "authorization_endpoint"), Required(root, "token_endpoint"),
            Optional(root, "userinfo_endpoint"), Required(root, "jwks_uri"));
        // OpenID Connect Discovery 1.0 §4.3: the issuer must be the authority the document came from.
        if (configuration.Issuer.TrimEnd('/') != authority.TrimEnd('/') && !configuration.Issuer.Contains("{tenantid}", StringComparison.Ordinal))
            throw new InvalidOperationException($"Discovery for '{authority}' names a different issuer ('{configuration.Issuer}').");
        foreach (var url in new[] { configuration.AuthorizationEndpoint, configuration.TokenEndpoint, configuration.JwksUri })
            RequireHttps(url);
        _configurations[authority] = (configuration, now + CacheTime);
        return configuration;
    }

    /// <summary>The signing keys at <paramref name="jwksUri"/>; <paramref name="refresh"/> reloads them unless they were loaded moments ago.</summary>
    public async Task<IReadOnlyList<WebJsonWebKey>> GetKeysAsync(string jwksUri, bool refresh, CancellationToken cancellationToken)
    {
        var now = _time.GetUtcNow();
        if (_keys.TryGetValue(jwksUri, out var cached) && (refresh ? now - cached.LoadedAt < MinRefresh : now - cached.LoadedAt < CacheTime))
            return cached.Keys;

        using var document = await GetJsonAsync(jwksUri, cancellationToken).ConfigureAwait(false);
        var keys = document.RootElement.TryGetProperty("keys", out var array) && array.ValueKind == JsonValueKind.Array
            ? (IReadOnlyList<WebJsonWebKey>)[.. array.EnumerateArray().Select(WebJsonWebKey.FromJson).OfType<WebJsonWebKey>()]
            : [];
        _keys[jwksUri] = (keys, now);
        return keys;
    }

    internal static void RequireHttps(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
            throw new InvalidOperationException($"'{url}' is not an absolute https URL.");
    }

    private async Task<JsonDocument> GetJsonAsync(string url, CancellationToken cancellationToken)
    {
        RequireHttps(url);
        using var response = await _http.CreateClient(WebExternalLoginHttp.ClientName).GetAsync(new Uri(url), HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength > MaxDocumentBytes)
            throw new InvalidOperationException($"'{url}' returned a document that is too large.");
        return await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false), cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private static string Required(JsonElement root, string name) =>
        Optional(root, name) ?? throw new InvalidOperationException($"The discovery document has no '{name}'.");

    private static string? Optional(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
