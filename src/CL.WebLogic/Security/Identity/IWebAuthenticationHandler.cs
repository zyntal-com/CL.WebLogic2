using CL.WebLogic.Security.Pipeline;

namespace CL.WebLogic.Security.Identity;

/// <summary>
/// Establishes an identity from the request (session cookie, API key, bearer token…). The
/// authentication stage runs handlers in <see cref="Order"/>; the first success wins and a
/// failure (presented but invalid credentials) stops the request with 401.
/// </summary>
public interface IWebAuthenticationHandler
{
    /// <summary>Scheme name (see <see cref="WebAuthenticationSchemes"/>).</summary>
    string Scheme { get; }

    /// <summary>Position in the handler chain (lower runs first).</summary>
    int Order { get; }

    /// <summary>Attempts to authenticate the request.</summary>
    ValueTask<WebAuthenticationResult> AuthenticateAsync(WebSecurityContext context, CancellationToken cancellationToken);
}
