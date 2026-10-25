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
}
