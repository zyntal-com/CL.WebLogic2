namespace CL.WebLogic.Auth.Passkeys;

/// <summary>What a passkey's attestation statement proved at registration.</summary>
public enum WebAttestationType
{
    /// <summary>No attestation (<c>none</c>; the default when the site does not ask for one).</summary>
    None,

    /// <summary>Self attestation: signed with the credential key itself (packed, no certificate).</summary>
    Self,

    /// <summary>
    /// Signed by an attestation certificate whose signature and basic requirements were checked.
    /// The certificate chain is <b>not</b> evaluated against a trust store or the FIDO metadata service.
    /// </summary>
    Basic,

    /// <summary>A format WebLogic does not verify (tpm, android-key, apple…), accepted as no attestation.</summary>
    Unverified
}
