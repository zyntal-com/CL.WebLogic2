using System.Buffers.Text;
using System.Text;
using System.Text.Json;

namespace CL.WebLogic.ExternalLogins.Jwt;

/// <summary>
/// A compact JWS (<c>header.payload.signature</c>) split and decoded, not yet trusted. Use
/// <see cref="WebJwtValidator"/> before reading any claim.
/// </summary>
public sealed class WebJwt
{
    /// <summary>Longest token accepted (ID tokens are a few KB).</summary>
    public const int MaxLength = 16 * 1024;

    private WebJwt(JsonElement header, JsonElement payload, byte[] signingInput, byte[] signature)
    {
        Header = header;
        Payload = payload;
        SigningInput = signingInput;
        Signature = signature;
    }

    /// <summary>The JOSE header.</summary>
    public JsonElement Header { get; }

    /// <summary>The claims.</summary>
    public JsonElement Payload { get; }

    /// <summary>The bytes the signature covers (<c>header.payload</c> as ASCII).</summary>
    public byte[] SigningInput { get; }

    /// <summary>The signature bytes.</summary>
    public byte[] Signature { get; }

    /// <summary>The <c>alg</c> header.</summary>
    public string Algorithm => Header.TryGetProperty("alg", out var alg) && alg.ValueKind == JsonValueKind.String ? alg.GetString()! : string.Empty;

    /// <summary>The <c>kid</c> header, if any.</summary>
    public string? KeyId => Header.TryGetProperty("kid", out var kid) && kid.ValueKind == JsonValueKind.String ? kid.GetString() : null;

    /// <summary>Splits and decodes <paramref name="token"/>; false when it is not a well-formed compact JWS.</summary>
    public static bool TryParse(string? token, out WebJwt jwt)
    {
        jwt = null!;
        if (string.IsNullOrEmpty(token) || token.Length > MaxLength)
            return false;
        var parts = token.Split('.');
        if (parts.Length != 3 || parts[0].Length == 0 || parts[1].Length == 0 || parts[2].Length == 0)
            return false;
        try
        {
            var header = JsonDocument.Parse(Base64Url.DecodeFromChars(parts[0])).RootElement.Clone();
            var payload = JsonDocument.Parse(Base64Url.DecodeFromChars(parts[1])).RootElement.Clone();
            if (header.ValueKind != JsonValueKind.Object || payload.ValueKind != JsonValueKind.Object)
                return false;
            jwt = new WebJwt(header, payload, Encoding.ASCII.GetBytes(parts[0] + "." + parts[1]), Base64Url.DecodeFromChars(parts[2]));
            return true;
        }
        catch (Exception ex) when (ex is FormatException or JsonException)
        {
            return false;
        }
    }
}
