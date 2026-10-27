using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CL.WebLogic.Auth.Events;
using CL.WebLogic.Auth.Users;
using CL.WebLogic.Configuration.Services;
using CL.WebLogic.Hosting.Services;
using CL.WebLogic.Results;
using CL.WebLogic.Runtime.Models;
using CL.WebLogic.Runtime.Services;
using CL.WebLogic.Security.Sessions;
using CodeLogic.Core.Results;
using Microsoft.AspNetCore.DataProtection;

namespace CL.WebLogic.Auth.External;

/// <summary>
/// Runs external sign-in: <see cref="ChallengeAsync"/> stores state, nonce and return URL in a
/// protected 10-minute cookie and redirects; <see cref="HandleCallbackAsync"/> checks the state,
/// lets the provider verify the response and finds the linked user. The app maps the two routes
/// and decides about unlinked identities.
/// </summary>
public sealed class WebExternalLoginFlow
{
    private const string CookieName = "__Host-wl_ext";

    private readonly Dictionary<string, IWebExternalLoginProvider> _providers;
    private readonly IDataProtector _protector;
    private readonly IWebUserStore _users;
    private readonly WebLogicConfigHolder _config;
    private readonly TimeProvider _time;
    private readonly IWebLogicContext? _context;

    /// <summary>Creates the flow.</summary>
    public WebExternalLoginFlow(IEnumerable<IWebExternalLoginProvider> providers, IDataProtectionProvider protection, IWebUserStore users, WebLogicConfigHolder config,
        TimeProvider? time = null, IWebLogicContext? context = null)
    {
        _context = context;
        _providers = (providers ?? []).ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);
        _protector = (protection ?? throw new ArgumentNullException(nameof(protection))).CreateProtector("CL.WebLogic.Auth.External.v1");
        _users = users ?? throw new ArgumentNullException(nameof(users));
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _time = time ?? TimeProvider.System;
    }

    /// <summary>Registered providers.</summary>
    public IReadOnlyCollection<IWebExternalLoginProvider> Providers => _providers.Values;

    /// <summary>Redirects to <paramref name="provider"/>; it returns to <paramref name="callbackPath"/>.</summary>
    public async Task<WebResult> ChallengeAsync(WebRequestContext request, string provider, string callbackPath, string? returnUrl, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!_providers.TryGetValue(provider ?? string.Empty, out var p))
            return WebResult.Status(404);

        var challenge = new WebExternalChallenge
        {
            CallbackUrl = Absolute(request, callbackPath),
            State = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(16)),
            Nonce = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(16)),
            CodeVerifier = System.Buffers.Text.Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32))
        };
        var state = new FlowState(p.Name, challenge.State, challenge.Nonce, challenge.CallbackUrl, WebUrl.SafeReturnUrl(returnUrl),
            _time.GetUtcNow().AddMinutes(10).ToUnixTimeSeconds(), challenge.CodeVerifier);

        var http = request.HttpContext;
        http.Response.Cookies.Append(WebSessionCookies.Name(http, CookieName), _protector.Protect(JsonSerializer.Serialize(state)),
            WebSessionCookies.Options(http, _time.GetUtcNow().AddMinutes(10)));
        var url = await p.GetChallengeUrlAsync(challenge, cancellationToken).ConfigureAwait(false);
        if (!Uri.TryCreate(url, UriKind.Absolute, out var target) || target.Scheme is not ("https" or "http"))
            throw new InvalidOperationException($"External login provider '{p.Name}' returned an invalid challenge URL.");
        return WebResult.ExternalRedirect(target);
    }

    /// <summary>Verifies the callback for <paramref name="provider"/>.</summary>
    public async Task<Result<WebExternalLoginOutcome>> HandleCallbackAsync(WebRequestContext request, string provider, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var http = request.HttpContext;
        var cookieName = WebSessionCookies.Name(http, CookieName);
        var cookie = http.Request.Cookies[cookieName];
        if (!http.Response.HasStarted)
            http.Response.Cookies.Delete(cookieName, WebSessionCookies.Options(http, null));

        FlowState? state = null;
        try
        {
            if (cookie is not null)
                state = JsonSerializer.Deserialize<FlowState>(_protector.Unprotect(cookie));
        }
        catch (Exception ex) when (ex is CryptographicException or JsonException)
        {
            state = null;
        }

        var returned = Encoding.UTF8.GetBytes(request.GetQuery("state") ?? string.Empty);
        if (state is null || !string.Equals(state.Provider, provider, StringComparison.OrdinalIgnoreCase)
            || state.ExpiresAt < _time.GetUtcNow().ToUnixTimeSeconds()
            || !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(state.State), returned)
            || !_providers.TryGetValue(state.Provider, out var p))
            return Result<WebExternalLoginOutcome>.Failure(Error.Validation("weblogic.auth.external_state", "The sign-in attempt expired; try again."));

        var challenge = new WebExternalChallenge { CallbackUrl = state.CallbackUrl, State = state.State, Nonce = state.Nonce, CodeVerifier = state.CodeVerifier ?? string.Empty };
        var info = await p.HandleCallbackAsync(request, challenge, cancellationToken).ConfigureAwait(false);
        if (info.IsFailure)
            return Result<WebExternalLoginOutcome>.Failure(info.Error!);

        var user = _users is IWebUserExternalLoginStore links
            ? await links.FindByExternalLoginAsync(p.Name, info.Value!.ProviderKey, cancellationToken).ConfigureAwait(false)
            : null;
        return Result<WebExternalLoginOutcome>.Success(new WebExternalLoginOutcome { Info = info.Value!, User = user, ReturnUrl = state.ReturnUrl });
    }

    /// <summary>Links an external identity to a user (requires <see cref="IWebUserExternalLoginStore"/>); raises <see cref="ExternalLoginChangedEvent"/>.</summary>
    public async Task LinkAsync(WebUser user, WebExternalLoginInfo info, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(info);
        await Links().AddExternalLoginAsync(user.Id, info.Provider, info.ProviderKey, cancellationToken).ConfigureAwait(false);
        _context?.Events.Publish(new ExternalLoginChangedEvent(user.Id, info.Provider, Linked: true));
    }

    /// <summary>
    /// Unlinks the user's identity at <paramref name="provider"/> and raises <see cref="ExternalLoginChangedEvent"/>.
    /// Fails with <c>weblogic.auth.external_login_not_found</c> when there is no such link. Make sure the user keeps
    /// another way to sign in (a password, a passkey or another provider) before calling it.
    /// </summary>
    public async Task<Result> UnlinkAsync(WebUser user, string provider, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentException.ThrowIfNullOrWhiteSpace(provider);
        if (!await Links().RemoveExternalLoginAsync(user.Id, provider, cancellationToken).ConfigureAwait(false))
            return Result.Failure(Error.NotFound("weblogic.auth.external_login_not_found", "That account is not linked."));
        _context?.Events.Publish(new ExternalLoginChangedEvent(user.Id, provider, Linked: false));
        return Result.Success();
    }

    private IWebUserExternalLoginStore Links() =>
        _users as IWebUserExternalLoginStore ?? throw new InvalidOperationException("The user store does not implement IWebUserExternalLoginStore.");

    private string Absolute(WebRequestContext request, string path)
    {
        var baseUrl = _config.Current.PublicBaseUrl.TrimEnd('/');
        if (baseUrl.Length == 0)
        {
            var http = request.HttpContext.Request;
            baseUrl = http.Scheme + "://" + http.Host.Value + http.PathBase.Value;
        }
        return baseUrl + (path.StartsWith('/') ? path : "/" + path);
    }

    private sealed record FlowState(string Provider, string State, string Nonce, string CallbackUrl, string ReturnUrl, long ExpiresAt, string? CodeVerifier = null);
}
