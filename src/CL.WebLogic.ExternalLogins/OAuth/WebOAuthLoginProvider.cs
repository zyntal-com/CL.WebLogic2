using System.Text.Json;
using CL.WebLogic.Auth.External;
using CL.WebLogic.ExternalLogins.Jwt;
using CL.WebLogic.Hosting.Services;
using CL.WebLogic.Runtime.Models;
using CodeLogic.Core.Results;

namespace CL.WebLogic.ExternalLogins.OAuth;

/// <summary>
/// Sign-in with an OAuth 2.0 / OpenID Connect provider: authorization code flow with PKCE (S256),
/// <c>state</c> and <c>nonce</c> from <see cref="WebExternalLoginFlow"/>. OpenID Connect ID tokens
/// are validated in-house (signature from the issuer's JWKS, issuer, audience, expiry, nonce); plain
/// OAuth 2.0 providers are read from their user info endpoint. Failures are logged with the reason;
/// the user only sees a generic message.
/// </summary>
public sealed partial class WebOAuthLoginProvider : IWebExternalLoginProvider
{
    private readonly WebOAuthOptions _options;
    private readonly WebOidcMetadata _metadata;
    private readonly IHttpClientFactory _http;
    private readonly IWebLogicContext _context;
    private readonly TimeProvider _time;

    /// <summary>Creates the provider.</summary>
    public WebOAuthLoginProvider(WebOAuthOptions options, WebOidcMetadata metadata, IHttpClientFactory http, IWebLogicContext context, TimeProvider? time = null)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _options.Validate();
        _metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _time = time ?? TimeProvider.System;
    }

    /// <inheritdoc/>
    public string Name => _options.Name;

    /// <inheritdoc/>
    public string DisplayName => _options.DisplayName ?? _options.Name;

    /// <inheritdoc/>
    public async ValueTask<string> GetChallengeUrlAsync(WebExternalChallenge challenge, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(challenge);
        var endpoints = await EndpointsAsync(cancellationToken).ConfigureAwait(false);
        var parameters = new List<(string, string)>
        {
            ("response_type", "code"),
            ("client_id", _options.ClientId),
            ("redirect_uri", challenge.CallbackUrl),
            ("state", challenge.State)
        };
        if (_options.Scopes.Count > 0)
            parameters.Add(("scope", string.Join(' ', _options.Scopes)));
        if (endpoints.Oidc is not null)
            parameters.Add(("nonce", challenge.Nonce));
        if (_options.UsePkce)
        {
            if (challenge.CodeVerifier.Length < 43)
                throw new InvalidOperationException("PKCE needs the code verifier from WebExternalLoginFlow.");
            parameters.Add(("code_challenge", challenge.CodeChallenge));
            parameters.Add(("code_challenge_method", "S256"));
        }
        parameters.AddRange(_options.AuthorizationParameters
            .Where(p => !parameters.Any(existing => existing.Item1 == p.Key))
            .Select(p => (p.Key, p.Value)));

        var separator = endpoints.Authorization.Contains('?', StringComparison.Ordinal) ? "&" : "?";
        return endpoints.Authorization + separator + string.Join('&', parameters.Select(p => p.Item1 + "=" + Uri.EscapeDataString(p.Item2)));
    }

    /// <inheritdoc/>
    public async ValueTask<Result<WebExternalLoginInfo>> HandleCallbackAsync(WebRequestContext request, WebExternalChallenge challenge, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(challenge);
        if (request.GetQuery("error") is { Length: > 0 } error)
            return error is "access_denied" ? Fail("The sign-in was cancelled.") : Fail("The provider refused the sign-in.", "error=" + error);
        if (request.GetQuery("code") is not { Length: > 0 and <= 4096 } code)
            return Fail("The provider sent no authorization code.", "missing code");

        try
        {
            var endpoints = await EndpointsAsync(cancellationToken).ConfigureAwait(false);
            // RFC 9207: when the provider names itself, it must be the one we sent the user to.
            if (endpoints.Oidc is { } oidcIssuer && request.GetQuery("iss") is { Length: > 0 } iss && !IssuerMatches(iss, oidcIssuer.Issuer))
                return Fail("The sign-in response came from another provider.", "iss=" + iss);

            var tokens = await ExchangeCodeAsync(endpoints.Token, code, challenge, cancellationToken).ConfigureAwait(false);
            JsonElement profile;
            if (endpoints.Oidc is { } oidc)
            {
                var (claims, reason) = await ValidateIdTokenAsync(oidc, tokens.IdToken, challenge.Nonce, cancellationToken).ConfigureAwait(false);
                if (claims is null)
                    return Fail("The provider's identity token was not valid.", "id_token: " + reason);
                profile = claims.Value;
            }
            else
            {
                if (tokens.AccessToken is null)
                    return Fail("The provider sent no access token.", "missing access_token");
                profile = await GetJsonAsync(endpoints.UserInfo!, tokens.AccessToken, cancellationToken).ConfigureAwait(false);
            }
            return await ToInfoAsync(profile, tokens.AccessToken, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when ((ex is HttpRequestException or JsonException or InvalidOperationException or TaskCanceledException) && !cancellationToken.IsCancellationRequested)
        {
            return Fail("The provider could not be reached; try again.", ex.GetType().Name + ": " + ex.Message);
        }
    }

    private async Task<(JsonElement? Claims, string? Reason)> ValidateIdTokenAsync(WebOidcConfiguration oidc, string? idToken, string nonce, CancellationToken cancellationToken)
    {
        if (idToken is null)
            return (null, "missing");
        var parameters = new WebJwtValidationParameters
        {
            Issuer = oidc.Issuer, Audience = _options.ClientId, Nonce = nonce, Now = _time.GetUtcNow(), ClockSkew = _options.ClockSkew
        };
        var keys = await _metadata.GetKeysAsync(oidc.JwksUri, refresh: false, cancellationToken).ConfigureAwait(false);
        var result = WebJwtValidator.Validate(idToken, keys, parameters);
        if (result.Error == "unknown signing key")
        {
            keys = await _metadata.GetKeysAsync(oidc.JwksUri, refresh: true, cancellationToken).ConfigureAwait(false);
            result = WebJwtValidator.Validate(idToken, keys, parameters);
        }
        return result;
    }

    private static bool IssuerMatches(string returned, string expected) =>
        expected.Contains("{tenantid}", StringComparison.Ordinal)
            ? returned.StartsWith(expected[..expected.IndexOf("{tenantid}", StringComparison.Ordinal)], StringComparison.Ordinal)
            : returned == expected;

    private Result<WebExternalLoginInfo> Fail(string message, string? reason = null)
    {
        if (reason is not null)
            _context.Logger.Warning($"External sign-in with '{Name}' failed: {reason}");
        return Result<WebExternalLoginInfo>.Failure(Error.Unauthorized("weblogic.external.failed", message));
    }
}
