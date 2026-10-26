using System.Security.Cryptography;
using CL.WebLogic.Auth.Passkeys.Cbor;

namespace CL.WebLogic.Auth.Passkeys;

/// <summary>
/// A credential public key in COSE form (RFC 9053): EC2 keys on P-256/384/521 or RSA keys of at least
/// 2048 bits, each bound to one algorithm from <see cref="WebCoseAlgorithm"/>. Verifies WebAuthn
/// signatures (ECDSA signatures are DER-encoded there).
/// </summary>
public sealed class WebCoseKey
{
    private const int KeyTypeLabel = 1;
    private const int AlgorithmLabel = 3;
    private const int Ec2 = 2;
    private const int Rsa = 3;

    private readonly ECParameters? _ec;
    private readonly RSAParameters? _rsa;

    private WebCoseKey(int algorithm, ECParameters? ec, RSAParameters? rsa)
    {
        Algorithm = algorithm;
        _ec = ec;
        _rsa = rsa;
    }

    /// <summary>The COSE algorithm the key is bound to.</summary>
    public int Algorithm { get; }

    /// <summary>The EC public key, for EC2 keys.</summary>
    public ECParameters? EcParameters => _ec;

    /// <summary>The RSA public key, for RSA keys.</summary>
    public RSAParameters? RsaParameters => _rsa;

    /// <summary>Parses a COSE key; throws <see cref="WebCborException"/> when it is malformed or unsupported.</summary>
    public static WebCoseKey Parse(ReadOnlySpan<byte> cose) => Parse(WebCbor.Decode(cose));

    /// <summary>Parses a decoded COSE key.</summary>
    public static WebCoseKey Parse(WebCborValue key)
    {
        ArgumentNullException.ThrowIfNull(key);
        var algorithm = (int)key.Require(AlgorithmLabel).Number;
        if (!WebCoseAlgorithm.IsSupported(algorithm))
            throw new WebCborException($"Unsupported COSE algorithm {algorithm}.");

        switch (key.Require(KeyTypeLabel).Number)
        {
            case Ec2:
            {
                var curve = key.Require(-1).Number switch
                {
                    1 when algorithm == WebCoseAlgorithm.ES256 => ECCurve.NamedCurves.nistP256,
                    2 when algorithm == WebCoseAlgorithm.ES384 => ECCurve.NamedCurves.nistP384,
                    3 when algorithm == WebCoseAlgorithm.ES512 => ECCurve.NamedCurves.nistP521,
                    _ => throw new WebCborException("The COSE curve does not match the algorithm.")
                };
                var size = algorithm switch { WebCoseAlgorithm.ES256 => 32, WebCoseAlgorithm.ES384 => 48, _ => 66 };
                var x = key.Require(-2).Bytes;
                var y = key.Require(-3).Bytes;
                if (x.Length != size || y.Length != size)
                    throw new WebCborException("The COSE EC coordinates have the wrong length.");
                var parameters = new ECParameters { Curve = curve, Q = new ECPoint { X = x, Y = y } };
                try
                {
                    using var check = ECDsa.Create(parameters); // rejects points that are not on the curve
                }
                catch (Exception ex) when (ex is CryptographicException or PlatformNotSupportedException or ArgumentException)
                {
                    // Windows CNG reports an off-curve point as PlatformNotSupportedException.
                    throw new WebCborException("The COSE EC point is not valid.", ex);
                }
                return new WebCoseKey(algorithm, parameters, null);
            }
            case Rsa when algorithm is WebCoseAlgorithm.RS256 or WebCoseAlgorithm.PS256:
            {
                var modulus = key.Require(-1).Bytes;
                var exponent = key.Require(-2).Bytes;
                var bits = (modulus.Length - modulus.TakeWhile(b => b == 0).Count()) * 8;
                if (bits < 2048 || exponent.Length is 0 or > 8)
                    throw new WebCborException("RSA credential keys must be at least 2048 bits.");
                return new WebCoseKey(algorithm, null, new RSAParameters { Modulus = modulus, Exponent = exponent });
            }
            default:
                throw new WebCborException("The COSE key type does not match the algorithm.");
        }
    }

    /// <summary>True when <paramref name="signature"/> is this key's signature over <paramref name="data"/>.</summary>
    public bool Verify(ReadOnlySpan<byte> data, ReadOnlySpan<byte> signature)
    {
        try
        {
            if (_ec is { } ec)
            {
                using var ecdsa = ECDsa.Create(ec);
                return ecdsa.VerifyData(data, signature, Hash(Algorithm), DSASignatureFormat.Rfc3279DerSequence);
            }
            using var rsa = RSA.Create(_rsa!.Value);
            var padding = Algorithm == WebCoseAlgorithm.PS256 ? RSASignaturePadding.Pss : RSASignaturePadding.Pkcs1;
            return rsa.VerifyData(data, signature, HashAlgorithmName.SHA256, padding);
        }
        catch (CryptographicException)
        {
            return false;
        }
    }

    /// <summary>Encodes an EC public key as COSE (for test authenticators and fixtures).</summary>
    public static byte[] Encode(ECParameters key, int algorithm)
    {
        var crv = algorithm switch { WebCoseAlgorithm.ES256 => 1, WebCoseAlgorithm.ES384 => 2, WebCoseAlgorithm.ES512 => 3, _ => throw new ArgumentOutOfRangeException(nameof(algorithm)) };
        return new WebCborWriter().Map(5)
            .Number(KeyTypeLabel).Number(Ec2)
            .Number(AlgorithmLabel).Number(algorithm)
            .Number(-1).Number(crv)
            .Number(-2).Bytes(key.Q.X)
            .Number(-3).Bytes(key.Q.Y)
            .ToArray();
    }

    /// <summary>Encodes an RSA public key as COSE (for test authenticators and fixtures).</summary>
    public static byte[] Encode(RSAParameters key, int algorithm) =>
        new WebCborWriter().Map(4)
            .Number(KeyTypeLabel).Number(Rsa)
            .Number(AlgorithmLabel).Number(algorithm)
            .Number(-1).Bytes(key.Modulus)
            .Number(-2).Bytes(key.Exponent)
            .ToArray();

    internal static HashAlgorithmName Hash(int algorithm) => algorithm switch
    {
        WebCoseAlgorithm.ES384 => HashAlgorithmName.SHA384,
        WebCoseAlgorithm.ES512 => HashAlgorithmName.SHA512,
        _ => HashAlgorithmName.SHA256
    };
}
