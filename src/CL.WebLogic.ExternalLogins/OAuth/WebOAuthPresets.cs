namespace CL.WebLogic.ExternalLogins.OAuth;

/// <summary>
/// Ready-made <see cref="WebOAuthOptions"/> for common providers. Register the callback URL
/// (<c>{PublicBaseUrl}{callback path}</c>) at the provider; read the secret from the environment.
/// </summary>
public static class WebOAuthPresets
{
    /// <summary>Google (OpenID Connect; e-mail verified by Google).</summary>
    public static WebOAuthOptions Google(string clientId, string clientSecret) => new()
    {
        Name = "google",
        DisplayName = "Google",
        ClientId = clientId,
        ClientSecret = clientSecret,
        Authority = "https://accounts.google.com",
        Scopes = ["openid", "email", "profile"]
    };

    /// <summary>
    /// Microsoft identity platform (OpenID Connect). <paramref name="tenant"/>: <c>common</c> (work,
    /// school and personal accounts), <c>organizations</c>, <c>consumers</c> or a tenant id. Microsoft
    /// does not verify the <c>email</c> claim, so it is never reported as verified.
    /// </summary>
    public static WebOAuthOptions Microsoft(string clientId, string clientSecret, string tenant = "common") => new()
    {
        Name = "microsoft",
        DisplayName = "Microsoft",
        ClientId = clientId,
        ClientSecret = clientSecret,
        Authority = $"https://login.microsoftonline.com/{Uri.EscapeDataString(tenant)}/v2.0",
        Scopes = ["openid", "email", "profile"],
        EmailVerifiedClaim = null
    };

    /// <summary>GitHub (OAuth 2.0; the primary verified address comes from <c>/user/emails</c>).</summary>
    public static WebOAuthOptions GitHub(string clientId, string clientSecret) => new()
    {
        Name = "github",
        DisplayName = "GitHub",
        ClientId = clientId,
        ClientSecret = clientSecret,
        AuthorizationEndpoint = "https://github.com/login/oauth/authorize",
        TokenEndpoint = "https://github.com/login/oauth/access_token",
        UserInfoEndpoint = "https://api.github.com/user",
        EmailsEndpoint = "https://api.github.com/user/emails",
        Scopes = ["read:user", "user:email"],
        SubjectClaim = "id",
        NameClaim = "login",
        EmailVerifiedClaim = null
    };

    /// <summary>Discord (OAuth 2.0; <c>verified</c> says whether Discord verified the e-mail).</summary>
    public static WebOAuthOptions Discord(string clientId, string clientSecret) => new()
    {
        Name = "discord",
        DisplayName = "Discord",
        ClientId = clientId,
        ClientSecret = clientSecret,
        AuthorizationEndpoint = "https://discord.com/oauth2/authorize",
        TokenEndpoint = "https://discord.com/api/oauth2/token",
        UserInfoEndpoint = "https://discord.com/api/users/@me",
        Scopes = ["identify", "email"],
        SubjectClaim = "id",
        NameClaim = "username",
        EmailVerifiedClaim = "verified"
    };
}
