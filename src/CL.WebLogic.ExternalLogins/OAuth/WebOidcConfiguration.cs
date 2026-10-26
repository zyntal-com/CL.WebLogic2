namespace CL.WebLogic.ExternalLogins.OAuth;

/// <summary>The parts of an OpenID Connect discovery document the provider uses.</summary>
/// <param name="Issuer">The issuer (<c>iss</c> of its ID tokens).</param>
/// <param name="AuthorizationEndpoint">Where the browser is sent.</param>
/// <param name="TokenEndpoint">Where the code is exchanged.</param>
/// <param name="UserInfoEndpoint">The user info endpoint, if any.</param>
/// <param name="JwksUri">The signing keys.</param>
public sealed record WebOidcConfiguration(string Issuer, string AuthorizationEndpoint, string TokenEndpoint, string? UserInfoEndpoint, string JwksUri);
