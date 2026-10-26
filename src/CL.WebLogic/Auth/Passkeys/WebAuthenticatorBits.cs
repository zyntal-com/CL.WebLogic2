namespace CL.WebLogic.Auth.Passkeys;

/// <summary>The flag bits of the authenticator data (WebAuthn §6.1).</summary>
[Flags]
public enum WebAuthenticatorBits : byte
{
    /// <summary>No flags.</summary>
    None = 0,

    /// <summary>UP: the user was present (touched the authenticator).</summary>
    UserPresent = 0x01,

    /// <summary>UV: the user was verified (PIN, biometrics).</summary>
    UserVerified = 0x04,

    /// <summary>BE: the credential may be backed up (synced passkey).</summary>
    BackupEligible = 0x08,

    /// <summary>BS: the credential is currently backed up.</summary>
    BackedUp = 0x10,

    /// <summary>AT: attested credential data follows (registration).</summary>
    AttestedCredentialData = 0x40,

    /// <summary>ED: extension data follows.</summary>
    ExtensionData = 0x80
}
