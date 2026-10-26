using System.Security.Cryptography;
using CL.WebLogic.Auth.Passkeys;
using CL.WebLogic.Auth.Passkeys.Cbor;

namespace CL.WebLogic.Tests.Auth.Passkeys;

public sealed class CoseKeyTests
{
    private static readonly byte[] Data = "signed bytes"u8.ToArray();

    [Theory]
    [InlineData(WebCoseAlgorithm.ES256, 256)]
    [InlineData(WebCoseAlgorithm.ES384, 384)]
    [InlineData(WebCoseAlgorithm.ES512, 521)]
    public void EC_keys_verify_DER_signatures(int algorithm, int bits)
    {
        using var ec = ECDsa.Create(bits switch { 256 => ECCurve.NamedCurves.nistP256, 384 => ECCurve.NamedCurves.nistP384, _ => ECCurve.NamedCurves.nistP521 });
        var key = WebCoseKey.Parse(WebCoseKey.Encode(ec.ExportParameters(false), algorithm));
        var hash = WebCoseKey.Hash(algorithm);

        Assert.Equal(algorithm, key.Algorithm);
        Assert.True(key.Verify(Data, ec.SignData(Data, hash, DSASignatureFormat.Rfc3279DerSequence)));
        Assert.False(key.Verify(Data, ec.SignData(Data, hash, DSASignatureFormat.IeeeP1363FixedFieldConcatenation))); // raw r||s is not WebAuthn
        Assert.False(key.Verify("other bytes"u8, ec.SignData(Data, hash, DSASignatureFormat.Rfc3279DerSequence)));
        Assert.False(key.Verify(Data, [0x30, 0x00]));
    }

    [Theory]
    [InlineData(WebCoseAlgorithm.RS256)]
    [InlineData(WebCoseAlgorithm.PS256)]
    public void RSA_keys_verify_with_their_padding(int algorithm)
    {
        using var rsa = RSA.Create(2048);
        var key = WebCoseKey.Parse(WebCoseKey.Encode(rsa.ExportParameters(false), algorithm));
        var right = algorithm == WebCoseAlgorithm.PS256 ? RSASignaturePadding.Pss : RSASignaturePadding.Pkcs1;
        var wrong = algorithm == WebCoseAlgorithm.PS256 ? RSASignaturePadding.Pkcs1 : RSASignaturePadding.Pss;

        Assert.True(key.Verify(Data, rsa.SignData(Data, HashAlgorithmName.SHA256, right)));
        Assert.False(key.Verify(Data, rsa.SignData(Data, HashAlgorithmName.SHA256, wrong)));
    }

    [Fact]
    public void Mismatched_weak_or_invalid_keys_are_refused()
    {
        using var p256 = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var point = p256.ExportParameters(false);

        // ES384 claimed for a P-256 key.
        Assert.Throws<WebCborException>(() => WebCoseKey.Parse(Ec(point.Q.X!, point.Q.Y!, alg: WebCoseAlgorithm.ES384, crv: 1)));
        // Coordinates of the wrong size.
        Assert.Throws<WebCborException>(() => WebCoseKey.Parse(Ec(point.Q.X![..31], point.Q.Y!, WebCoseAlgorithm.ES256, 1)));
        // A point that is not on the curve.
        var offCurve = (byte[])point.Q.Y!.Clone();
        offCurve[^1] ^= 0x01;
        Assert.Throws<WebCborException>(() => WebCoseKey.Parse(Ec(point.Q.X!, offCurve, WebCoseAlgorithm.ES256, 1)));
        // EdDSA (unsupported) and an RSA key claiming ES256.
        Assert.Throws<WebCborException>(() => WebCoseKey.Parse(Ec(point.Q.X!, point.Q.Y!, -8, 1)));
        using var rsa = RSA.Create(2048);
        Assert.Throws<WebCborException>(() => WebCoseKey.Parse(WebCoseKey.Encode(rsa.ExportParameters(false), WebCoseAlgorithm.ES256)));
        // RSA under 2048 bits.
        using var weak = RSA.Create(1024);
        Assert.Throws<WebCborException>(() => WebCoseKey.Parse(WebCoseKey.Encode(weak.ExportParameters(false), WebCoseAlgorithm.RS256)));
    }

    private static byte[] Ec(byte[] x, byte[] y, int alg, int crv) =>
        new WebCborWriter().Map(5).Number(1).Number(2).Number(3).Number(alg).Number(-1).Number(crv).Number(-2).Bytes(x).Number(-3).Bytes(y).ToArray();
}
