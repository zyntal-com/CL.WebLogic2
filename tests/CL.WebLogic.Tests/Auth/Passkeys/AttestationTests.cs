using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text.Json;
using CL.WebLogic.Auth.Passkeys;
using CL.WebLogic.Auth.Passkeys.Cbor;
using CL.WebLogic.Testing.Services;

namespace CL.WebLogic.Tests.Auth.Passkeys;

/// <summary>Authenticator data and attestation statements, built by <see cref="WebTestAuthenticator"/>.</summary>
public sealed class AttestationTests
{
    private static readonly JsonElement Options = JsonDocument.Parse("""
        {
          "challenge": "Y2hhbGxlbmdlLWJ5dGVzLTAxMjM0NTY3ODk",
          "rp": { "id": "site.test", "name": "Site" },
          "user": { "id": "dXNlci1oYW5kbGU", "name": "ada", "displayName": "Ada" },
          "pubKeyCredParams": [ { "type": "public-key", "alg": -7 }, { "type": "public-key", "alg": -257 }, { "type": "public-key", "alg": -37 } ]
        }
        """).RootElement;

    private sealed record Parsed(string Format, WebCborValue Statement, WebAuthenticatorData Data, byte[] ClientDataHash, WebCoseKey Key);

    private static Parsed Register(WebTestAttestation attestation, int algorithm = WebCoseAlgorithm.ES256)
    {
        using var authenticator = new WebTestAuthenticator("https://site.test") { Attestation = attestation, Algorithm = algorithm };
        var response = authenticator.Create(Options).GetProperty("response");
        var attestationObject = WebCbor.Decode(Base64Url.DecodeFromChars(response.GetProperty("attestationObject").GetString()));
        var data = WebAuthenticatorData.Parse(attestationObject.Require("authData").Bytes);
        return new Parsed(attestationObject.Require("fmt").Text, attestationObject.Require("attStmt"), data,
            SHA256.HashData(Base64Url.DecodeFromChars(response.GetProperty("clientDataJSON").GetString())), WebCoseKey.Parse(data.CredentialPublicKey!));
    }

    private static (WebAttestationType? Type, string? Error) Verify(Parsed p, WebCborValue? statement = null, byte[]? clientDataHash = null, bool allowUnverified = false) =>
        WebAttestationVerifier.Verify(p.Format, statement ?? p.Statement, p.Data, clientDataHash ?? p.ClientDataHash, p.Key, allowUnverified);

    [Fact]
    public void Authenticator_data_carries_the_attested_credential()
    {
        var p = Register(WebTestAttestation.None);
        Assert.Equal(SHA256.HashData("site.test"u8), p.Data.RpIdHash);
        Assert.True(p.Data.Has(WebAuthenticatorBits.UserPresent | WebAuthenticatorBits.UserVerified | WebAuthenticatorBits.AttestedCredentialData));
        Assert.Equal(32, p.Data.CredentialId!.Length);
        Assert.Equal(new Guid("2a7d3f1c-0b5e-4c6a-9d8e-7f6a5b4c3d2e"), p.Data.Aaguid);
        Assert.Equal(WebCoseAlgorithm.ES256, p.Key.Algorithm);
    }

    [Fact]
    public void Malformed_authenticator_data_is_refused()
    {
        var raw = Register(WebTestAttestation.None).Data.Raw;
        Assert.Throws<WebCborException>(() => WebAuthenticatorData.Parse(raw[..36]));
        Assert.Throws<WebCborException>(() => WebAuthenticatorData.Parse([.. raw, 0x00]));          // trailing byte
        Assert.Throws<WebCborException>(() => WebAuthenticatorData.Parse(raw[..^5]));             // truncated key
        var longId = (byte[])raw.Clone();
        longId[53] = 0x04; // credential id length 0x04xx > 1023
        Assert.Throws<WebCborException>(() => WebAuthenticatorData.Parse(longId));
    }

    [Theory]
    [InlineData(WebTestAttestation.None, WebAttestationType.None)]
    [InlineData(WebTestAttestation.PackedSelf, WebAttestationType.Self)]
    [InlineData(WebTestAttestation.PackedCertificate, WebAttestationType.Basic)]
    [InlineData(WebTestAttestation.FidoU2f, WebAttestationType.Basic)]
    public void Supported_formats_verify(WebTestAttestation attestation, WebAttestationType expected)
    {
        var (type, error) = Verify(Register(attestation));
        Assert.Null(error);
        Assert.Equal(expected, type);
    }

    [Theory]
    [InlineData(WebCoseAlgorithm.RS256)]
    [InlineData(WebCoseAlgorithm.PS256)]
    public void Packed_self_attestation_works_for_RSA(int algorithm) =>
        Assert.Equal(WebAttestationType.Self, Verify(Register(WebTestAttestation.PackedSelf, algorithm)).Type);

    [Theory]
    [InlineData(WebTestAttestation.PackedSelf)]
    [InlineData(WebTestAttestation.PackedCertificate)]
    [InlineData(WebTestAttestation.FidoU2f)]
    public void A_signature_over_other_client_data_fails(WebTestAttestation attestation)
    {
        var p = Register(attestation);
        Assert.NotNull(Verify(p, clientDataHash: SHA256.HashData("other"u8)).Error);
    }

    [Fact]
    public void Packed_certificates_must_meet_the_requirements()
    {
        var p = Register(WebTestAttestation.PackedCertificate);
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var signature = key.SignData([.. p.Data.Raw, .. p.ClientDataHash], HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence);
        WebCborValue Statement(byte[] certificate) => WebCbor.Decode(new WebCborWriter().Map(3)
            .Text("alg").Number(WebCoseAlgorithm.ES256).Text("sig").Bytes(signature).Text("x5c").Array(1).Bytes(certificate).ToArray());

        const string Good = "C=US, O=X, OU=Authenticator Attestation, CN=Y";
        Assert.Null(Verify(p, Statement(WebTestAuthenticator.AttestationCertificate(key, Good, p.Data.Aaguid))).Error);
        Assert.Contains("OU", Verify(p, Statement(WebTestAuthenticator.AttestationCertificate(key, "C=US, O=X, OU=Other, CN=Y", p.Data.Aaguid))).Error);
        Assert.Contains("CA", Verify(p, Statement(WebTestAuthenticator.AttestationCertificate(key, Good, p.Data.Aaguid, certificateAuthority: true))).Error);
        Assert.Contains("AAGUID", Verify(p, Statement(WebTestAuthenticator.AttestationCertificate(key, Good, Guid.NewGuid()))).Error);
        Assert.Null(Verify(p, Statement(WebTestAuthenticator.AttestationCertificate(key, Good, aaguid: null))).Error); // the extension is optional
    }

    [Fact]
    public void Self_attestation_must_use_the_credential_algorithm_and_key()
    {
        var p = Register(WebTestAttestation.PackedSelf);
        var forged = WebCbor.Decode(new WebCborWriter().Map(2).Text("alg").Number(WebCoseAlgorithm.RS256).Text("sig").Bytes(p.Statement.Require("sig").Bytes).ToArray());
        Assert.NotNull(Verify(p, forged).Error);
    }

    [Fact]
    public void None_must_be_empty_and_unknown_formats_follow_the_policy()
    {
        var p = Register(WebTestAttestation.None);
        var notEmpty = WebCbor.Decode(new WebCborWriter().Map(1).Text("sig").Bytes([1]).ToArray());
        Assert.NotNull(Verify(p, notEmpty).Error);

        var tpm = p with { Format = "tpm" };
        Assert.Contains("unsupported", Verify(tpm).Error);
        Assert.Equal(WebAttestationType.Unverified, Verify(tpm, allowUnverified: true).Type);
        Assert.NotNull(Verify(p, WebCbor.Decode([0x01])).Error); // statement must be a map
    }

    [Fact]
    public void Client_data_parses_and_rejects_junk()
    {
        var data = WebClientData.TryParse("""{"type":"webauthn.get","challenge":"abc","origin":"https://site.test","crossOrigin":true}"""u8.ToArray())!;
        Assert.Equal(("webauthn.get", "abc", "https://site.test", true), (data.Type, data.Challenge, data.Origin, data.CrossOrigin));
        Assert.Equal(SHA256.HashData(data.Raw), data.Hash);
        Assert.Null(WebClientData.TryParse("[1]"u8.ToArray()));
        Assert.Null(WebClientData.TryParse("not json"u8.ToArray()));
        Assert.Null(WebClientData.TryParse([]));
    }
}
