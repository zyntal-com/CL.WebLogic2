using CL.WebLogic.Runtime.Models;
using CodeLogic.Core.Results;

namespace CL.WebLogic.Auth.External;

/// <summary>
/// An external identity provider (Steam OpenID, an OpenID Connect issuer, GitHub…). The provider
/// builds the redirect and verifies the callback; <see cref="WebExternalLoginFlow"/> handles state,
/// nonce and the return URL.
/// </summary>
public interface IWebExternalLoginProvider
{
    /// <summary>Id used in URLs and stored links, e.g. <c>steam</c>.</summary>
    string Name { get; }

    /// <summary>Button text.</summary>
    string DisplayName { get; }

    /// <summary>
    /// Origins the challenge redirects the browser to (e.g. <c>https://steamcommunity.com</c>). They are added to the
    /// site's <c>form-action</c>, because browsers apply it to the redirect that follows a form post: a sign-in or link
    /// started with a POST form (the CSRF-safe way) is blocked otherwise. Empty by default.
    /// </summary>
    IReadOnlyList<string> RedirectOrigins => [];

    /// <summary>The provider URL to redirect the browser to.</summary>
    ValueTask<string> GetChallengeUrlAsync(WebExternalChallenge challenge, CancellationToken cancellationToken);

    /// <summary>Verifies the callback request (after the flow checked the state) and returns who signed in.</summary>
    ValueTask<Result<WebExternalLoginInfo>> HandleCallbackAsync(WebRequestContext request, WebExternalChallenge challenge, CancellationToken cancellationToken);
}
