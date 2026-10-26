namespace CL.WebLogic.Auth.Passkeys;

/// <summary>A registered passkey as the app's <see cref="IWebPasskeyStore"/> keeps it. Nothing in it is secret.</summary>
public sealed record WebPasskey
{
    /// <summary>The credential id (base64url), unique across all users.</summary>
    public required string CredentialId { get; init; }

    /// <summary>The owner.</summary>
    public required string UserId { get; init; }

    /// <summary>The WebAuthn user handle (base64url, random per user, the same for all of their passkeys).</summary>
    public required string UserHandle { get; init; }

    /// <summary>The public key in COSE form.</summary>
    public required byte[] PublicKey { get; init; }

    /// <summary>The COSE algorithm (<see cref="WebCoseAlgorithm"/>).</summary>
    public int Algorithm { get; init; }

    /// <summary>The last signature counter seen (0 when the authenticator keeps none).</summary>
    public long SignCount { get; init; }

    /// <summary>A name the user chose ("Work laptop").</summary>
    public string Name { get; init; } = "Passkey";

    /// <summary>Transports the browser reported (internal, hybrid, usb, nfc, ble), for sign-in hints.</summary>
    public IReadOnlyList<string> Transports { get; init; } = [];

    /// <summary>The authenticator model (all zeros when not disclosed).</summary>
    public Guid Aaguid { get; init; }

    /// <summary>True for synced passkeys (may be backed up).</summary>
    public bool BackupEligible { get; init; }

    /// <summary>True when the passkey was backed up at its last use.</summary>
    public bool BackedUp { get; init; }

    /// <summary>The attestation format at registration (<c>none</c>, <c>packed</c>…).</summary>
    public string AttestationFormat { get; init; } = "none";

    /// <summary>What the attestation proved.</summary>
    public WebAttestationType AttestationType { get; init; }

    /// <summary>When it was registered.</summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>When it was last used to sign in.</summary>
    public DateTimeOffset? LastUsedAt { get; init; }
}
