using CL.WebLogic.Security.Identity;
using CL.WebLogic.Security.Pipeline;

namespace CL.WebLogic.Auth.Handlers;

/// <summary>
/// Validates bearer tokens (JWTs from an identity provider, opaque tokens…). Supplied by the app;
/// the toolkit does not ship a JWT library.
/// </summary>
public interface IWebBearerTokenValidator
{
    /// <summary>The identity for <paramref name="token"/>, or null when the token is not valid.</summary>
    ValueTask<WebIdentity?> ValidateAsync(string token, WebSecurityContext context, CancellationToken cancellationToken);
}
