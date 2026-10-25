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

    /// <summary>The provider URL to redirect the browser to.</summary>
    ValueTask<string> GetChallengeUrlAsync(WebExternalChallenge challenge, CancellationToken cancellationToken);

    /// <summary>Verifies the callback request (after the flow checked the state) and returns who signed in.</summary>
    ValueTask<Result<WebExternalLoginInfo>> HandleCallbackAsync(WebRequestContext request, WebExternalChallenge challenge, CancellationToken cancellationToken);
}
