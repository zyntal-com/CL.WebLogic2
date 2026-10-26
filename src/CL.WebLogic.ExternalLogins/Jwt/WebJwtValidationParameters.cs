namespace CL.WebLogic.ExternalLogins.Jwt;

/// <summary>What an ID token must satisfy.</summary>
public sealed class WebJwtValidationParameters
{
    /// <summary>
    /// The expected <c>iss</c>. A <c>{tenantid}</c> placeholder (Microsoft's multi-tenant
    /// endpoints) is replaced with the token's own <c>tid</c> claim before comparing.
    /// </summary>
    public required string Issuer { get; init; }

    /// <summary>The client id that must be in <c>aud</c> (and in <c>azp</c> when there are several audiences).</summary>
    public required string Audience { get; init; }

    /// <summary>The nonce sent with the authorization request, or null to skip the check.</summary>
    public string? Nonce { get; init; }

    /// <summary>The time to validate at.</summary>
    public required DateTimeOffset Now { get; init; }

    /// <summary>Tolerated clock difference.</summary>
    public TimeSpan ClockSkew { get; init; } = TimeSpan.FromMinutes(5);
}
