namespace CL.WebLogic.Testing.Services;

/// <summary>The attestation statement a <see cref="WebTestAuthenticator"/> produces.</summary>
public enum WebTestAttestation
{
    /// <summary><c>none</c> (what browsers send unless the site asks for attestation).</summary>
    None,

    /// <summary><c>packed</c> self attestation (signed with the credential key).</summary>
    PackedSelf,

    /// <summary><c>packed</c> with a freshly made attestation certificate.</summary>
    PackedCertificate,

    /// <summary><c>fido-u2f</c> (a security key; ES256 only).</summary>
    FidoU2f
}
