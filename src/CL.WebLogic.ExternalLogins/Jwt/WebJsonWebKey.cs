using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text.Json;

namespace CL.WebLogic.ExternalLogins.Jwt;

/// <summary>
/// One public signing key from a JWKS (RSA or EC). Verifies JWS signatures for the asymmetric
/// algorithms only: RS256/384/512, PS256/384/512 and ES256/384/512.
/// </summary>
public sealed class WebJsonWebKey
{
    private readonly RSAParameters? _rsa;
    private readonly ECParameters? _ec;

    private WebJsonWebKey(string? keyId, string? algorithm, RSAParameters? rsa, ECParameters? ec)
    {
        KeyId = keyId;
        Algorithm = algorithm;
        _rsa = rsa;
        _ec = ec;
    }

    /// <summary>The key id (<c>kid</c>).</summary>
    public string? KeyId { get; }

    /// <summary>The algorithm the key is restricted to (<c>alg</c>), if any.</summary>
    public string? Algorithm { get; }

    /// <summary>Reads a JWK; null when it is not a usable public signing key.</summary>
    public static WebJsonWebKey? FromJson(JsonElement jwk)
    {
        if (jwk.ValueKind != JsonValueKind.Object)
            return null;
        if (Text(jwk, "use") is { } use && use != "sig")
            return null;
        var kid = Text(jwk, "kid");
        var alg = Text(jwk, "alg");
        try
        {
            return Text(jwk, "kty") switch
            {
                "RSA" when Bytes(jwk, "n") is { Length: >= 256 } n && Bytes(jwk, "e") is { Length: > 0 } e =>
                    new WebJsonWebKey(kid, alg, new RSAParameters { Modulus = n, Exponent = e }, null),
                "EC" when Curve(Text(jwk, "crv")) is { } curve && Bytes(jwk, "x") is { } x && Bytes(jwk, "y") is { } y =>
                    new WebJsonWebKey(kid, alg, null, new ECParameters { Curve = curve, Q = new ECPoint { X = x, Y = y } }),
                _ => null
            };
        }
        catch (FormatException)
        {
            return null;
        }
    }

    /// <summary>True when the signature of <paramref name="jwt"/> verifies with this key under its <c>alg</c>.</summary>
    public bool Verify(WebJwt jwt)
    {
        ArgumentNullException.ThrowIfNull(jwt);
        var alg = jwt.Algorithm;
        if (Algorithm is not null && Algorithm != alg)
            return false;
        try
        {
            switch (alg)
            {
                case "RS256" or "RS384" or "RS512" or "PS256" or "PS384" or "PS512" when _rsa is { } rsaParameters:
                {
                    using var rsa = RSA.Create(rsaParameters);
                    var padding = alg[0] == 'P' ? RSASignaturePadding.Pss : RSASignaturePadding.Pkcs1;
                    return rsa.VerifyData(jwt.SigningInput, jwt.Signature, Hash(alg), padding);
                }
                case "ES256" or "ES384" or "ES512" when _ec is { } ecParameters && CurveFor(alg) == ecParameters.Curve.Oid.Value:
                {
                    using var ec = ECDsa.Create(ecParameters);
                    return ec.VerifyData(jwt.SigningInput, jwt.Signature, Hash(alg), DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
                }
                default:
                    return false; // "none", HMAC and anything unknown are never accepted
            }
        }
        catch (CryptographicException)
        {
            return false;
        }
    }

    private static HashAlgorithmName Hash(string alg) => alg[2..] switch
    {
        "256" => HashAlgorithmName.SHA256,
        "384" => HashAlgorithmName.SHA384,
        _ => HashAlgorithmName.SHA512
    };

    private static string? CurveFor(string alg) => alg switch
    {
        "ES256" => ECCurve.NamedCurves.nistP256.Oid.Value,
        "ES384" => ECCurve.NamedCurves.nistP384.Oid.Value,
        "ES512" => ECCurve.NamedCurves.nistP521.Oid.Value,
        _ => null
    };

    private static ECCurve? Curve(string? crv) => crv switch
    {
        "P-256" => ECCurve.NamedCurves.nistP256,
        "P-384" => ECCurve.NamedCurves.nistP384,
        "P-521" => ECCurve.NamedCurves.nistP521,
        _ => null
    };

    private static string? Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static byte[]? Bytes(JsonElement element, string name) =>
        Text(element, name) is { Length: > 0 } text ? Base64Url.DecodeFromChars(text) : null;
}
