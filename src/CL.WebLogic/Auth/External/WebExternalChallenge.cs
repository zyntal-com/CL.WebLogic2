namespace CL.WebLogic.Auth.External;

/// <summary>One external sign-in attempt.</summary>
public sealed class WebExternalChallenge
{
    /// <summary>Absolute callback URL (from <c>PublicBaseUrl</c> when set).</summary>
    public required string CallbackUrl { get; init; }

    /// <summary>Random state, echoed back by the provider (checked by the flow).</summary>
    public required string State { get; init; }

    /// <summary>Random nonce for providers that support one (OpenID Connect).</summary>
    public required string Nonce { get; init; }

    /// <summary>
    /// PKCE code verifier (RFC 7636): 43 random URL-safe characters, kept in the flow's protected
    /// cookie and never sent to the browser. Send <see cref="CodeChallenge"/> with the redirect and
    /// this value with the token request.
    /// </summary>
    public string CodeVerifier { get; init; } = string.Empty;

    /// <summary>The S256 PKCE challenge for <see cref="CodeVerifier"/> (<c>code_challenge_method=S256</c>).</summary>
    public string CodeChallenge => System.Buffers.Text.Base64Url.EncodeToString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.ASCII.GetBytes(CodeVerifier)));
}
