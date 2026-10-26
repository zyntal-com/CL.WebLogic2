using System.Buffers.Binary;
using CL.WebLogic.Auth.Passkeys.Cbor;

namespace CL.WebLogic.Auth.Passkeys;

/// <summary>
/// Parsed authenticator data (WebAuthn §6.1): RP id hash, flags, signature counter and, on
/// registration, the attested credential (AAGUID, credential id, COSE public key). Throws
/// <see cref="WebCborException"/> on malformed input.
/// </summary>
public sealed class WebAuthenticatorData
{
    /// <summary>Longest credential id WebAuthn allows.</summary>
    public const int MaxCredentialIdLength = 1023;

    private WebAuthenticatorData(byte[] raw)
    {
        Raw = raw;
    }

    /// <summary>The bytes as sent (they are signed).</summary>
    public byte[] Raw { get; }

    /// <summary>SHA-256 of the RP id the credential is scoped to.</summary>
    public byte[] RpIdHash { get; private init; } = [];

    /// <summary>The flags.</summary>
    public WebAuthenticatorBits Flags { get; private init; }

    /// <summary>The signature counter (0 when the authenticator does not keep one).</summary>
    public uint SignCount { get; private init; }

    /// <summary>The authenticator model (registration only; all zeros when not disclosed).</summary>
    public Guid Aaguid { get; private init; }

    /// <summary>The credential id (registration only).</summary>
    public byte[]? CredentialId { get; private init; }

    /// <summary>The credential public key as COSE bytes (registration only).</summary>
    public byte[]? CredentialPublicKey { get; private init; }

    /// <summary>True when <paramref name="flag"/> is set.</summary>
    public bool Has(WebAuthenticatorBits flag) => (Flags & flag) == flag;

    /// <summary>Parses authenticator data.</summary>
    public static WebAuthenticatorData Parse(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (data.Length < 37)
            throw new WebCborException("Authenticator data is too short.");

        var flags = (WebAuthenticatorBits)data[32];
        var position = 37;
        Guid aaguid = default;
        byte[]? credentialId = null, publicKey = null;

        if ((flags & WebAuthenticatorBits.AttestedCredentialData) != 0)
        {
            if (data.Length < position + 18)
                throw new WebCborException("Attested credential data is truncated.");
            aaguid = new Guid(data.AsSpan(position, 16), bigEndian: true);
            var idLength = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(position + 16, 2));
            position += 18;
            if (idLength is 0 or > MaxCredentialIdLength || data.Length < position + idLength)
                throw new WebCborException("The credential id has an invalid length.");
            credentialId = data.AsSpan(position, idLength).ToArray();
            position += idLength;
            WebCbor.Decode(data.AsSpan(position), out var keyLength);
            publicKey = data.AsSpan(position, keyLength).ToArray();
            position += keyLength;
        }

        if ((flags & WebAuthenticatorBits.ExtensionData) != 0)
        {
            var extensions = WebCbor.Decode(data.AsSpan(position), out var read);
            if (extensions.Type != WebCborType.Map)
                throw new WebCborException("Authenticator extensions must be a map.");
            position += read;
        }

        if (position != data.Length)
            throw new WebCborException("Unexpected bytes after the authenticator data.");

        return new WebAuthenticatorData(data)
        {
            RpIdHash = data[..32],
            Flags = flags,
            SignCount = BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(33, 4)),
            Aaguid = aaguid,
            CredentialId = credentialId,
            CredentialPublicKey = publicKey
        };
    }
}
