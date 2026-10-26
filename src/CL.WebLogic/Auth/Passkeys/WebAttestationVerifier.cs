using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using CL.WebLogic.Auth.Passkeys.Cbor;

namespace CL.WebLogic.Auth.Passkeys;

/// <summary>
/// Verifies attestation statements (WebAuthn §8): <c>none</c>, <c>packed</c> (self and x5c) and
/// <c>fido-u2f</c>. Other formats are <see cref="WebAttestationType.Unverified"/> when allowed.
/// Certificate chains are not evaluated (there is no trust store or metadata service here).
/// </summary>
public static class WebAttestationVerifier
{
    private const string AaguidExtensionOid = "1.3.6.1.4.1.45724.1.1.4";
    private const string OrganizationalUnitOid = "2.5.4.11";

    /// <summary>The attestation type, or the reason the statement is not valid.</summary>
    public static (WebAttestationType? Type, string? Error) Verify(string format, WebCborValue statement, WebAuthenticatorData authenticatorData,
        byte[] clientDataHash, WebCoseKey credentialKey, bool allowUnverifiedFormats)
    {
        ArgumentNullException.ThrowIfNull(statement);
        ArgumentNullException.ThrowIfNull(authenticatorData);
        ArgumentNullException.ThrowIfNull(clientDataHash);
        ArgumentNullException.ThrowIfNull(credentialKey);
        if (statement.Type != WebCborType.Map)
            return (null, "attStmt is not a map");
        try
        {
            return format switch
            {
                "none" => statement.Entries.Count == 0 ? (WebAttestationType.None, null) : (null, "none attestation with a statement"),
                "packed" => Packed(statement, authenticatorData, clientDataHash, credentialKey),
                "fido-u2f" => FidoU2f(statement, authenticatorData, clientDataHash, credentialKey),
                _ => allowUnverifiedFormats ? (WebAttestationType.Unverified, null) : (null, $"unsupported attestation format '{format}'")
            };
        }
        catch (Exception ex) when (ex is WebCborException or CryptographicException)
        {
            return (null, "malformed attestation statement: " + ex.Message);
        }
    }

    private static (WebAttestationType?, string?) Packed(WebCborValue statement, WebAuthenticatorData data, byte[] clientDataHash, WebCoseKey key)
    {
        var algorithm = (int)statement.Require("alg").Number;
        var signature = statement.Require("sig").Bytes;
        byte[] signed = [.. data.Raw, .. clientDataHash];

        if (statement.Get("x5c") is not { } x5c)
        {
            if (algorithm != key.Algorithm)
                return (null, "self attestation algorithm differs from the credential key");
            return key.Verify(signed, signature) ? (WebAttestationType.Self, null) : (null, "bad self attestation signature");
        }

        using var certificate = Leaf(x5c);
        if (certificate.Version != 3)
            return (null, "attestation certificate is not X.509 v3");
        if (certificate.Extensions[OidBasicConstraints] is X509BasicConstraintsExtension { CertificateAuthority: true })
            return (null, "attestation certificate is a CA");
        if (!certificate.SubjectName.EnumerateRelativeDistinguishedNames().Any(r =>
                r.GetSingleElementType().Value == OrganizationalUnitOid && r.GetSingleElementValue() == "Authenticator Attestation"))
            return (null, "attestation certificate subject has no OU=Authenticator Attestation");
        if (certificate.Extensions[AaguidExtensionOid] is { } extension
            && (extension.Critical || extension.RawData is not [0x04, 0x10, ..] || extension.RawData.Length != 18
                || new Guid(extension.RawData.AsSpan(2), bigEndian: true) != data.Aaguid))
            return (null, "attestation certificate AAGUID does not match");
        return VerifyWithCertificate(certificate, algorithm, signed, signature) ? (WebAttestationType.Basic, null) : (null, "bad attestation signature");
    }

    private static (WebAttestationType?, string?) FidoU2f(WebCborValue statement, WebAuthenticatorData data, byte[] clientDataHash, WebCoseKey key)
    {
        var x5c = statement.Require("x5c");
        if (x5c.Items.Count != 1)
            return (null, "fido-u2f needs exactly one certificate");
        if (key.Algorithm != WebCoseAlgorithm.ES256 || key.EcParameters is not { } point)
            return (null, "fido-u2f credentials must be ES256");
        using var certificate = Leaf(x5c);
        using var certificateKey = certificate.GetECDsaPublicKey();
        if (certificateKey is null || certificateKey.KeySize != 256)
            return (null, "fido-u2f certificate key must be P-256");

        byte[] signed = [0x00, .. data.RpIdHash, .. clientDataHash, .. data.CredentialId!, 0x04, .. point.Q.X!, .. point.Q.Y!];
        return certificateKey.VerifyData(signed, statement.Require("sig").Bytes, HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence)
            ? (WebAttestationType.Basic, null)
            : (null, "bad fido-u2f signature");
    }

    private static X509Certificate2 Leaf(WebCborValue x5c)
    {
        if (x5c.Items.Count == 0)
            throw new WebCborException("x5c is empty");
        return X509CertificateLoader.LoadCertificate(x5c.Items[0].Bytes);
    }

    private static bool VerifyWithCertificate(X509Certificate2 certificate, int algorithm, byte[] signed, byte[] signature)
    {
        switch (algorithm)
        {
            case WebCoseAlgorithm.ES256 or WebCoseAlgorithm.ES384 or WebCoseAlgorithm.ES512:
            {
                using var ec = certificate.GetECDsaPublicKey();
                return ec is not null && ec.VerifyData(signed, signature, WebCoseKey.Hash(algorithm), DSASignatureFormat.Rfc3279DerSequence);
            }
            case WebCoseAlgorithm.RS256 or WebCoseAlgorithm.PS256:
            {
                using var rsa = certificate.GetRSAPublicKey();
                return rsa is not null && rsa.KeySize >= 2048 && rsa.VerifyData(signed, signature, HashAlgorithmName.SHA256,
                    algorithm == WebCoseAlgorithm.PS256 ? RSASignaturePadding.Pss : RSASignaturePadding.Pkcs1);
            }
            default:
                return false;
        }
    }

    private const string OidBasicConstraints = "2.5.29.19";
}
