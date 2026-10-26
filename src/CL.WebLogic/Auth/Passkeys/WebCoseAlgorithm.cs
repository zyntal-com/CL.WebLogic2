namespace CL.WebLogic.Auth.Passkeys;

/// <summary>The COSE signature algorithms WebLogic verifies (IANA COSE Algorithms registry).</summary>
public static class WebCoseAlgorithm
{
    /// <summary>ECDSA P-256 with SHA-256 (the passkey default).</summary>
    public const int ES256 = -7;

    /// <summary>ECDSA P-384 with SHA-384.</summary>
    public const int ES384 = -35;

    /// <summary>ECDSA P-521 with SHA-512.</summary>
    public const int ES512 = -36;

    /// <summary>RSASSA-PSS with SHA-256.</summary>
    public const int PS256 = -37;

    /// <summary>RSASSA-PKCS1-v1_5 with SHA-256 (Windows Hello).</summary>
    public const int RS256 = -257;

    /// <summary>All supported algorithms, in preference order (sent as <c>pubKeyCredParams</c>).</summary>
    public static IReadOnlyList<int> Supported { get; } = [ES256, RS256, PS256, ES384, ES512];

    /// <summary>True when <paramref name="algorithm"/> is supported.</summary>
    public static bool IsSupported(int algorithm) => Supported.Contains(algorithm);
}
