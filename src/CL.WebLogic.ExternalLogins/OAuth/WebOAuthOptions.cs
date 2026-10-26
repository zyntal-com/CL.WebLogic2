namespace CL.WebLogic.ExternalLogins.OAuth;

/// <summary>
/// One OAuth 2.0 / OpenID Connect provider. With <see cref="Authority"/> the endpoints come from
/// discovery and the user comes from the validated ID token (OpenID Connect); without it, set the
/// endpoints and the user comes from <see cref="UserInfoEndpoint"/> (plain OAuth 2.0, e.g. GitHub).
/// Presets: <see cref="WebOAuthPresets"/>.
/// </summary>
public sealed record WebOAuthOptions
{
    /// <summary>Id used in URLs and stored links, e.g. <c>google</c> (lower-case letters, digits, <c>-</c>).</summary>
    public required string Name { get; init; }

    /// <summary>Button text.</summary>
    public string? DisplayName { get; init; }

    /// <summary>The client id registered at the provider.</summary>
    public required string ClientId { get; init; }

    /// <summary>The client secret (read it from the environment or a secret store, never from source).</summary>
    public string? ClientSecret { get; init; }

    /// <summary>OpenID Connect issuer, e.g. <c>https://accounts.google.com</c> (discovery at <c>/.well-known/openid-configuration</c>).</summary>
    public string? Authority { get; init; }

    /// <summary>Authorization endpoint (overrides discovery).</summary>
    public string? AuthorizationEndpoint { get; init; }

    /// <summary>Token endpoint (overrides discovery).</summary>
    public string? TokenEndpoint { get; init; }

    /// <summary>User info endpoint; used for the profile when there is no ID token.</summary>
    public string? UserInfoEndpoint { get; init; }

    /// <summary>
    /// GitHub-style e-mail list (<c>[{ "email", "primary", "verified" }]</c>), used to find the
    /// primary verified address when the profile has none.
    /// </summary>
    public string? EmailsEndpoint { get; init; }

    /// <summary>Scopes to request.</summary>
    public IReadOnlyList<string> Scopes { get; init; } = [];

    /// <summary>Send a PKCE S256 challenge (leave on unless the provider rejects it).</summary>
    public bool UsePkce { get; init; } = true;

    /// <summary>How the client authenticates at the token endpoint.</summary>
    public WebOAuthClientAuthentication ClientAuthentication { get; init; } = WebOAuthClientAuthentication.Post;

    /// <summary>Claim holding the stable user id (strings and numbers are accepted).</summary>
    public string SubjectClaim { get; init; } = "sub";

    /// <summary>Claim holding the display name.</summary>
    public string NameClaim { get; init; } = "name";

    /// <summary>Claim holding the e-mail address.</summary>
    public string EmailClaim { get; init; } = "email";

    /// <summary>Claim saying the e-mail is verified (null = never trust the e-mail as verified).</summary>
    public string? EmailVerifiedClaim { get; init; } = "email_verified";

    /// <summary>Extra authorization request parameters (e.g. <c>prompt=select_account</c>).</summary>
    public IReadOnlyDictionary<string, string> AuthorizationParameters { get; init; } = new Dictionary<string, string>();

    /// <summary>Tolerated clock difference for ID tokens.</summary>
    public TimeSpan ClockSkew { get; init; } = TimeSpan.FromMinutes(5);

    /// <summary>Throws when the options cannot work.</summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Name) || !Name.All(c => char.IsAsciiLetterLower(c) || char.IsAsciiDigit(c) || c == '-'))
            throw new ArgumentException($"OAuth provider name '{Name}' must be lower-case letters, digits or '-'.");
        if (string.IsNullOrWhiteSpace(ClientId))
            throw new ArgumentException($"OAuth provider '{Name}' needs a ClientId.");
        if (Authority is null && (AuthorizationEndpoint is null || TokenEndpoint is null || UserInfoEndpoint is null))
            throw new ArgumentException($"OAuth provider '{Name}' needs an Authority (OpenID Connect) or the authorization, token and user info endpoints.");
        foreach (var url in new[] { Authority, AuthorizationEndpoint, TokenEndpoint, UserInfoEndpoint, EmailsEndpoint })
        {
            if (url is not null && (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps))
                throw new ArgumentException($"OAuth provider '{Name}': '{url}' must be an absolute https URL.");
        }
    }
}
