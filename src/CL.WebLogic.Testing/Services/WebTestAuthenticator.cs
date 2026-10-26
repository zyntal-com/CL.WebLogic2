using System.Buffers.Binary;
using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using CL.WebLogic.Auth.Passkeys;
using CL.WebLogic.Auth.Passkeys.Cbor;

namespace CL.WebLogic.Testing.Services;

/// <summary>
/// A software WebAuthn authenticator plus browser for tests: turns the options a site sends into the
/// JSON a browser would post back (<c>PublicKeyCredential.toJSON()</c>), with real keys and
/// signatures. Change <see cref="Origin"/>, <see cref="UserVerified"/> or a credential's
/// <see cref="WebTestCredential.SignCount"/> to exercise the failure paths.
/// </summary>
public sealed partial class WebTestAuthenticator : IDisposable
{
    private readonly List<WebTestCredential> _credentials = [];

    /// <summary>Creates an authenticator that runs ceremonies on pages of <paramref name="origin"/>.</summary>
    public WebTestAuthenticator(string origin) => Origin = origin ?? throw new ArgumentNullException(nameof(origin));

    /// <summary>The page origin written into client data.</summary>
    public string Origin { get; set; }

    /// <summary>Key algorithm for new credentials (must be among the site's <c>pubKeyCredParams</c>).</summary>
    public int Algorithm { get; set; } = WebCoseAlgorithm.ES256;

    /// <summary>The attestation statement for new credentials.</summary>
    public WebTestAttestation Attestation { get; set; } = WebTestAttestation.None;

    /// <summary>Whether the user is verified (UV); a site requiring verification fails otherwise.</summary>
    public bool UserVerified { get; set; } = true;

    /// <summary>Whether credentials are synced passkeys (BE and BS flags).</summary>
    public bool Synced { get; set; } = true;

    /// <summary>Counter increment per assertion (0 = no counter, like most synced passkeys).</summary>
    public uint SignCountStep { get; set; }

    /// <summary>The AAGUID reported at registration.</summary>
    public Guid Aaguid { get; set; } = new("2a7d3f1c-0b5e-4c6a-9d8e-7f6a5b4c3d2e");

    /// <summary>Credentials created so far.</summary>
    public IReadOnlyList<WebTestCredential> Credentials => _credentials;

    /// <summary>Runs <c>navigator.credentials.create</c>: <paramref name="options"/> is the site's creation options JSON.</summary>
    public JsonElement Create(JsonElement options)
    {
        var rpId = options.GetProperty("rp").TryGetProperty("id", out var id) ? id.GetString()! : new Uri(Origin).Host;
        var userHandle = Base64Url.DecodeFromChars(options.GetProperty("user").GetProperty("id").GetString());
        if (!options.GetProperty("pubKeyCredParams").EnumerateArray().Any(p => p.GetProperty("alg").GetInt32() == Algorithm))
            throw new InvalidOperationException("NotSupportedError: the site does not accept the authenticator's algorithm.");
        if (options.TryGetProperty("excludeCredentials", out var exclude)
            && exclude.EnumerateArray().Any(c => _credentials.Any(own => Base64Url.EncodeToString(own.Id) == c.GetProperty("id").GetString())))
            throw new InvalidOperationException("InvalidStateError: the authenticator already holds a credential for this account.");
        RequireVerification(options.TryGetProperty("authenticatorSelection", out var selection) ? selection : default);

        var credential = new WebTestCredential(RandomNumberGenerator.GetBytes(32), rpId, userHandle, Algorithm, NewKey(Algorithm));
        var clientData = ClientData("webauthn.create", options);
        var aaguid = Attestation == WebTestAttestation.FidoU2f ? Guid.Empty : Aaguid;
        var publicKey = PublicKey(credential);
        byte[] attested = [.. aaguid.ToByteArray(bigEndian: true), (byte)(credential.Id.Length >> 8), (byte)credential.Id.Length, .. credential.Id, .. publicKey];
        var authenticatorData = AuthenticatorData(rpId, WebAuthenticatorBits.AttestedCredentialData, 0, attested);
        var attestationObject = new WebCborWriter().Map(3)
            .Text("fmt").Text(Format())
            .Text("attStmt").Raw(Statement(credential, authenticatorData, SHA256.HashData(clientData), rpId))
            .Text("authData").Bytes(authenticatorData)
            .ToArray();
        _credentials.Add(credential);

        return Json(new JsonObject
        {
            ["id"] = Base64Url.EncodeToString(credential.Id),
            ["rawId"] = Base64Url.EncodeToString(credential.Id),
            ["type"] = "public-key",
            ["authenticatorAttachment"] = "platform",
            ["clientExtensionResults"] = new JsonObject(),
            ["response"] = new JsonObject
            {
                ["clientDataJSON"] = Base64Url.EncodeToString(clientData),
                ["attestationObject"] = Base64Url.EncodeToString(attestationObject),
                ["transports"] = new JsonArray("internal", "hybrid")
            }
        });
    }

    /// <summary>
    /// Runs <c>navigator.credentials.get</c>: picks a credential from <c>allowCredentials</c>, or any
    /// credential for the RP id when the list is empty (discoverable sign-in).
    /// </summary>
    public JsonElement Get(JsonElement options)
    {
        var rpId = options.TryGetProperty("rpId", out var id) ? id.GetString()! : new Uri(Origin).Host;
        var allowed = options.TryGetProperty("allowCredentials", out var allow)
            ? allow.EnumerateArray().Select(c => c.GetProperty("id").GetString()).ToHashSet()
            : [];
        var credential = _credentials.LastOrDefault(c => c.RpId == rpId && (allowed.Count == 0 || allowed.Contains(Base64Url.EncodeToString(c.Id))))
            ?? throw new InvalidOperationException("NotAllowedError: no matching credential.");
        RequireVerification(options);

        credential.SignCount += SignCountStep;
        var clientData = ClientData("webauthn.get", options);
        var authenticatorData = AuthenticatorData(rpId, WebAuthenticatorBits.None, credential.SignCount, []);
        var signature = Sign(credential.Key, credential.Algorithm, [.. authenticatorData, .. SHA256.HashData(clientData)]);

        return Json(new JsonObject
        {
            ["id"] = Base64Url.EncodeToString(credential.Id),
            ["rawId"] = Base64Url.EncodeToString(credential.Id),
            ["type"] = "public-key",
            ["authenticatorAttachment"] = "platform",
            ["clientExtensionResults"] = new JsonObject(),
            ["response"] = new JsonObject
            {
                ["clientDataJSON"] = Base64Url.EncodeToString(clientData),
                ["authenticatorData"] = Base64Url.EncodeToString(authenticatorData),
                ["signature"] = Base64Url.EncodeToString(signature),
                ["userHandle"] = Base64Url.EncodeToString(credential.UserHandle)
            }
        });
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        foreach (var credential in _credentials)
            credential.Dispose();
        _credentials.Clear();
    }

    private void RequireVerification(JsonElement options)
    {
        if (options.ValueKind == JsonValueKind.Object && options.TryGetProperty("userVerification", out var uv) && uv.GetString() == "required" && !UserVerified)
            throw new InvalidOperationException("NotAllowedError: user verification is required.");
    }

    private byte[] ClientData(string type, JsonElement options) => Encoding.UTF8.GetBytes(new JsonObject
    {
        ["type"] = type,
        ["challenge"] = options.GetProperty("challenge").GetString(),
        ["origin"] = Origin,
        ["crossOrigin"] = false
    }.ToJsonString());

    private byte[] AuthenticatorData(string rpId, WebAuthenticatorBits extra, uint signCount, byte[] attested)
    {
        var flags = WebAuthenticatorBits.UserPresent | extra;
        if (UserVerified)
            flags |= WebAuthenticatorBits.UserVerified;
        if (Synced)
            flags |= WebAuthenticatorBits.BackupEligible | WebAuthenticatorBits.BackedUp;
        var counter = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(counter, signCount);
        return [.. SHA256.HashData(Encoding.UTF8.GetBytes(rpId)), (byte)flags, .. counter, .. attested];
    }

    private static JsonElement Json(JsonObject value) => JsonDocument.Parse(value.ToJsonString()).RootElement.Clone();
}
