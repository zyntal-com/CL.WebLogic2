using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace CL.WebLogic.ExternalLogins.Jwt;

/// <summary>
/// Validates an OpenID Connect ID token: an asymmetric signature from the issuer's key set, then
/// <c>iss</c>, <c>aud</c>/<c>azp</c>, <c>exp</c>/<c>nbf</c>/<c>iat</c> and <c>nonce</c>.
/// Returns the reason on failure (for logs; show users a generic message).
/// </summary>
public static class WebJwtValidator
{
    /// <summary>The claims when <paramref name="token"/> is valid; otherwise null and the reason.</summary>
    public static (JsonElement? Claims, string? Error) Validate(string? token, IReadOnlyList<WebJsonWebKey> keys, WebJwtValidationParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(keys);
        ArgumentNullException.ThrowIfNull(parameters);
        if (!WebJwt.TryParse(token, out var jwt))
            return (null, "malformed token");
        if (jwt.Header.TryGetProperty("crit", out _))
            return (null, "unsupported critical header");

        var candidates = jwt.KeyId is { } kid ? keys.Where(k => k.KeyId == kid).ToList() : [.. keys];
        if (candidates.Count == 0)
            return (null, "unknown signing key");
        if (!candidates.Any(k => k.Verify(jwt)))
            return (null, $"bad signature or algorithm '{jwt.Algorithm}'");

        var claims = jwt.Payload;
        var issuer = parameters.Issuer.Contains("{tenantid}", StringComparison.Ordinal)
            ? Text(claims, "tid") is { Length: > 0 } tid ? parameters.Issuer.Replace("{tenantid}", tid, StringComparison.Ordinal) : null
            : parameters.Issuer;
        if (issuer is null || Text(claims, "iss") != issuer)
            return (null, "wrong issuer");

        var audiences = Audiences(claims);
        if (!audiences.Contains(parameters.Audience, StringComparer.Ordinal))
            return (null, "wrong audience");
        if (audiences.Count > 1 && Text(claims, "azp") != parameters.Audience)
            return (null, "wrong authorized party");
        if (Text(claims, "azp") is { } azp && azp != parameters.Audience)
            return (null, "wrong authorized party");

        var now = parameters.Now.ToUnixTimeSeconds();
        var skew = (long)parameters.ClockSkew.TotalSeconds;
        if (Number(claims, "exp") is not { } exp || now > exp + skew)
            return (null, "expired");
        if (Number(claims, "nbf") is { } nbf && now + skew < nbf)
            return (null, "not yet valid");
        if (Number(claims, "iat") is { } iat && now + skew < iat)
            return (null, "issued in the future");

        if (parameters.Nonce is not null
            && (Text(claims, "nonce") is not { } nonce
                || !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(nonce), Encoding.UTF8.GetBytes(parameters.Nonce))))
            return (null, "wrong nonce");
        return (claims, null);
    }

    private static List<string> Audiences(JsonElement claims) =>
        !claims.TryGetProperty("aud", out var aud) ? []
        : aud.ValueKind == JsonValueKind.String ? [aud.GetString()!]
        : aud.ValueKind == JsonValueKind.Array ? [.. aud.EnumerateArray().Where(a => a.ValueKind == JsonValueKind.String).Select(a => a.GetString()!)]
        : [];

    private static string? Text(JsonElement claims, string name) =>
        claims.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static long? Number(JsonElement claims, string name) =>
        claims.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var number) ? (long)number : null;
}
