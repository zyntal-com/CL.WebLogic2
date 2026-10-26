using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using CL.WebLogic.Auth.Passkeys;
using CL.WebLogic.Auth.Passkeys.Cbor;

namespace CL.WebLogic.Testing.Services;

public sealed partial class WebTestAuthenticator
{
    private static AsymmetricAlgorithm NewKey(int algorithm) => algorithm switch
    {
        WebCoseAlgorithm.ES256 => ECDsa.Create(ECCurve.NamedCurves.nistP256),
        WebCoseAlgorithm.ES384 => ECDsa.Create(ECCurve.NamedCurves.nistP384),
        WebCoseAlgorithm.ES512 => ECDsa.Create(ECCurve.NamedCurves.nistP521),
        WebCoseAlgorithm.RS256 or WebCoseAlgorithm.PS256 => RSA.Create(2048),
        _ => throw new NotSupportedException($"Algorithm {algorithm} is not supported by the test authenticator.")
    };

    private static byte[] PublicKey(WebTestCredential credential) => credential.Key switch
    {
        ECDsa ec => WebCoseKey.Encode(ec.ExportParameters(false), credential.Algorithm),
        RSA rsa => WebCoseKey.Encode(rsa.ExportParameters(false), credential.Algorithm),
        _ => throw new NotSupportedException()
    };

    /// <summary>A WebAuthn signature: DER for ECDSA, PKCS#1 v1.5 or PSS for RSA.</summary>
    private static byte[] Sign(AsymmetricAlgorithm key, int algorithm, byte[] data) => key switch
    {
        ECDsa ec => ec.SignData(data, algorithm switch
        {
            WebCoseAlgorithm.ES384 => HashAlgorithmName.SHA384,
            WebCoseAlgorithm.ES512 => HashAlgorithmName.SHA512,
            _ => HashAlgorithmName.SHA256
        }, DSASignatureFormat.Rfc3279DerSequence),
        RSA rsa => rsa.SignData(data, HashAlgorithmName.SHA256, algorithm == WebCoseAlgorithm.PS256 ? RSASignaturePadding.Pss : RSASignaturePadding.Pkcs1),
        _ => throw new NotSupportedException()
    };

    private string Format() => Attestation switch
    {
        WebTestAttestation.None => "none",
        WebTestAttestation.FidoU2f => "fido-u2f",
        _ => "packed"
    };

    private byte[] Statement(WebTestCredential credential, byte[] authenticatorData, byte[] clientDataHash, string rpId)
    {
        switch (Attestation)
        {
            case WebTestAttestation.None:
                return new WebCborWriter().Map(0).ToArray();
            case WebTestAttestation.PackedSelf:
                return new WebCborWriter().Map(2)
                    .Text("alg").Number(credential.Algorithm)
                    .Text("sig").Bytes(Sign(credential.Key, credential.Algorithm, [.. authenticatorData, .. clientDataHash]))
                    .ToArray();
            case WebTestAttestation.PackedCertificate:
            {
                using var attestationKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
                var certificate = AttestationCertificate(attestationKey, "C=US, O=WebLogic Tests, OU=Authenticator Attestation, CN=WebLogic Test Authenticator", Aaguid);
                return new WebCborWriter().Map(3)
                    .Text("alg").Number(WebCoseAlgorithm.ES256)
                    .Text("sig").Bytes(Sign(attestationKey, WebCoseAlgorithm.ES256, [.. authenticatorData, .. clientDataHash]))
                    .Text("x5c").Array(1).Bytes(certificate)
                    .ToArray();
            }
            default:
            {
                if (credential.Key is not ECDsa ec || credential.Algorithm != WebCoseAlgorithm.ES256)
                    throw new InvalidOperationException("fido-u2f attestation needs an ES256 credential.");
                using var attestationKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
                var certificate = AttestationCertificate(attestationKey, "CN=WebLogic Test U2F", aaguid: null);
                var point = ec.ExportParameters(false).Q;
                byte[] signed = [0x00, .. SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(rpId)), .. clientDataHash, .. credential.Id, 0x04, .. point.X!, .. point.Y!];
                return new WebCborWriter().Map(2)
                    .Text("sig").Bytes(Sign(attestationKey, WebCoseAlgorithm.ES256, signed))
                    .Text("x5c").Array(1).Bytes(certificate)
                    .ToArray();
            }
        }
    }

    /// <summary>A self-signed attestation certificate (packed requirements: v3, not a CA, AAGUID extension).</summary>
    public static byte[] AttestationCertificate(ECDsa key, string subject, Guid? aaguid, bool certificateAuthority = false)
    {
        ArgumentNullException.ThrowIfNull(key);
        var request = new CertificateRequest(subject, key, HashAlgorithmName.SHA256);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(certificateAuthority, false, 0, true));
        if (aaguid is { } id)
            request.CertificateExtensions.Add(new X509Extension("1.3.6.1.4.1.45724.1.1.4", [0x04, 0x10, .. id.ToByteArray(bigEndian: true)], false));
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));
        return certificate.RawData;
    }
}
