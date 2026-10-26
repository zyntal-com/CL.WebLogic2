using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace CL.WebLogic.ExternalLogins.Tests;

/// <summary>Signs test JWTs and publishes the matching JWKS.</summary>
internal sealed class TestSigner : IDisposable
{
    public TestSigner(string kid = "rsa-1", int rsaBits = 2048)
    {
        Kid = kid;
        Rsa = RSA.Create(rsaBits);
        Ec = ECDsa.Create(ECCurve.NamedCurves.nistP256);
    }

    public string Kid { get; }

    public RSA Rsa { get; }

    public ECDsa Ec { get; }

    public string EcKid => Kid + "-ec";

    /// <summary>The public keys as a JWKS document.</summary>
    public string Jwks()
    {
        var rsa = Rsa.ExportParameters(false);
        var ec = Ec.ExportParameters(false);
        return new JsonObject
        {
            ["keys"] = new JsonArray(
                new JsonObject { ["kty"] = "RSA", ["kid"] = Kid, ["use"] = "sig", ["n"] = B64(rsa.Modulus!), ["e"] = B64(rsa.Exponent!) },
                new JsonObject { ["kty"] = "EC", ["kid"] = EcKid, ["crv"] = "P-256", ["x"] = B64(ec.Q.X!), ["y"] = B64(ec.Q.Y!) })
        }.ToJsonString();
    }

    /// <summary>A signed token; <paramref name="alg"/> RS256, PS256, ES256, HS256 (signed with the RSA modulus) or none.</summary>
    public string Sign(object claims, string alg = "RS256", string? kid = null, object? extraHeader = null)
    {
        var header = new JsonObject { ["alg"] = alg, ["typ"] = "JWT" };
        if ((kid ?? (alg.StartsWith("ES", StringComparison.Ordinal) ? EcKid : Kid)) is { } k)
            header["kid"] = k;
        if (extraHeader is not null)
            foreach (var (name, value) in JsonSerializer.SerializeToNode(extraHeader)!.AsObject())
                header[name] = value?.DeepClone();
        var input = B64(Encoding.UTF8.GetBytes(header.ToJsonString())) + "." + B64(JsonSerializer.SerializeToUtf8Bytes(claims));
        var data = Encoding.ASCII.GetBytes(input);
        var signature = alg switch
        {
            "RS256" => Rsa.SignData(data, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1),
            "PS256" => Rsa.SignData(data, HashAlgorithmName.SHA256, RSASignaturePadding.Pss),
            "ES256" => Ec.SignData(data, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation),
            "HS256" => HMACSHA256.HashData(Rsa.ExportParameters(false).Modulus!, data),
            _ => [0]
        };
        return input + "." + B64(signature);
    }

    public static string B64(byte[] bytes) => Base64Url.EncodeToString(bytes);

    public void Dispose()
    {
        Rsa.Dispose();
        Ec.Dispose();
    }
}
