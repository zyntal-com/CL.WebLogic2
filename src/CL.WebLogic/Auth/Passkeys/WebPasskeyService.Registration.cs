using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using CL.WebLogic.Auth.Events;
using CL.WebLogic.Auth.Notifications;
using CL.WebLogic.Auth.Passkeys.Cbor;
using CL.WebLogic.Auth.Users;
using CL.WebLogic.Security.Pipeline;
using CodeLogic.Core.Results;

namespace CL.WebLogic.Auth.Passkeys;

public sealed partial class WebPasskeyService
{
    private static readonly HashSet<string> KnownTransports = new(StringComparer.Ordinal) { "usb", "nfc", "ble", "smart-card", "hybrid", "internal" };

    /// <summary>
    /// Starts registering a passkey for <paramref name="user"/>: a discoverable credential with user
    /// verification, excluding the authenticators the user already registered.
    /// </summary>
    public async Task<Result<WebPasskeyChallenge>> BeginRegistrationAsync(WebSecurityContext security, WebUser user, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(security);
        ArgumentNullException.ThrowIfNull(user);
        if (!Settings.Enabled)
            return Result<WebPasskeyChallenge>.Failure(Error.Validation("weblogic.passkeys.disabled", "Passkeys are turned off."));
        var existing = await _store.ListAsync(user.Id, cancellationToken).ConfigureAwait(false);
        if (existing.Count >= Settings.MaxPerUser)
            return Result<WebPasskeyChallenge>.Failure(Error.Validation("weblogic.passkeys.limit", $"You can have at most {Settings.MaxPerUser} passkeys."));

        var rpId = RelyingPartyId(security);
        var userHandle = existing.Count > 0 ? existing[0].UserHandle : Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));
        var (challenge, state) = NewState("create", rpId, user.Id, userHandle, requireVerification: true);
        var options = new JsonObject
        {
            ["rp"] = new JsonObject { ["id"] = rpId, ["name"] = RelyingPartyName() },
            ["user"] = new JsonObject { ["id"] = userHandle, ["name"] = user.Email ?? user.UserName, ["displayName"] = user.UserName },
            ["challenge"] = challenge,
            ["pubKeyCredParams"] = new JsonArray([.. WebCoseAlgorithm.Supported.Select(a => (JsonNode)new JsonObject { ["type"] = "public-key", ["alg"] = a })]),
            ["timeout"] = Settings.TimeoutSeconds * 1000,
            ["excludeCredentials"] = Descriptors(existing),
            ["authenticatorSelection"] = new JsonObject { ["residentKey"] = "required", ["requireResidentKey"] = true, ["userVerification"] = "required" },
            ["attestation"] = Settings.RequestAttestation ? "direct" : "none",
            ["extensions"] = new JsonObject { ["credProps"] = true }
        };
        return Result<WebPasskeyChallenge>.Success(new WebPasskeyChallenge(JsonDocument.Parse(options.ToJsonString()).RootElement.Clone(), state));
    }

    /// <summary>Verifies the browser's answer and stores the passkey; the user is notified.</summary>
    public async Task<Result<WebPasskey>> CompleteRegistrationAsync(WebSecurityContext security, WebUser user, WebPasskeyRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(security);
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(request);
        var state = await SpendStateAsync(request.State, "create", cancellationToken).ConfigureAwait(false);
        if (state is null || state.UserId != user.Id)
            return Fail<WebPasskey>(security, "registration", "expired, reused or foreign state", "The request expired; try again.");

        var credential = request.Credential;
        var response = credential.ValueKind == JsonValueKind.Object && credential.TryGetProperty("response", out var r) ? r : default;
        var id = Text(credential, "id");
        var clientDataBytes = Decode(Text(response, "clientDataJSON"));
        var attestationBytes = Decode(Text(response, "attestationObject"));
        if (Text(credential, "type") != "public-key" || id is null || Text(credential, "rawId") is { } rawId && rawId != id
            || clientDataBytes is null || attestationBytes is null)
            return Fail<WebPasskey>(security, "registration", "malformed credential");

        var clientData = WebClientData.TryParse(clientDataBytes);
        if (CheckClientData(clientData, "webauthn.create", state, security) is { } clientProblem)
            return Fail<WebPasskey>(security, "registration", clientProblem);

        WebAuthenticatorData data;
        WebCoseKey key;
        string format;
        WebCborValue statement;
        try
        {
            var attestation = WebCbor.Decode(attestationBytes);
            format = attestation.Require("fmt").Text;
            statement = attestation.Require("attStmt");
            data = WebAuthenticatorData.Parse(attestation.Require("authData").Bytes);
            if (data.CredentialId is null || data.CredentialPublicKey is null)
                return Fail<WebPasskey>(security, "registration", "no attested credential data");
            key = WebCoseKey.Parse(data.CredentialPublicKey);
        }
        catch (WebCborException ex)
        {
            return Fail<WebPasskey>(security, "registration", "malformed attestation: " + ex.Message);
        }

        if (CheckAuthenticatorData(data, state) is { } dataProblem)
            return Fail<WebPasskey>(security, "registration", dataProblem);
        if (Base64Url.EncodeToString(data.CredentialId) != id)
            return Fail<WebPasskey>(security, "registration", "credential id differs from the authenticator data");

        var (attestationType, attestationError) = WebAttestationVerifier.Verify(format, statement, data, clientData!.Hash, key, allowUnverifiedFormats: !Settings.RequestAttestation);
        if (attestationType is null)
            return Fail<WebPasskey>(security, "registration", attestationError!);

        var now = _time.GetUtcNow();
        var passkey = new WebPasskey
        {
            CredentialId = id,
            UserId = user.Id,
            UserHandle = state.UserHandle!,
            PublicKey = data.CredentialPublicKey,
            Algorithm = key.Algorithm,
            SignCount = data.SignCount,
            Name = CleanName(request.Name),
            Transports = Transports(response),
            Aaguid = data.Aaguid,
            BackupEligible = data.Has(WebAuthenticatorBits.BackupEligible),
            BackedUp = data.Has(WebAuthenticatorBits.BackedUp),
            AttestationFormat = format,
            AttestationType = attestationType.Value,
            CreatedAt = now
        };
        if ((await _store.ListAsync(user.Id, cancellationToken).ConfigureAwait(false)).Count >= Settings.MaxPerUser)
            return Fail<WebPasskey>(security, "registration", "passkey limit reached", $"You can have at most {Settings.MaxPerUser} passkeys.");
        if (!await _store.AddAsync(passkey, cancellationToken).ConfigureAwait(false))
            return Fail<WebPasskey>(security, "registration", "credential id already registered", "This passkey is already registered.");

        _context.Events.Publish(new PasskeyChangedEvent(user.Id, id, Added: true));
        _notifications.Enqueue(user, WebSecurityNotificationKind.PasskeyAdded, values: new Dictionary<string, string> { ["name"] = passkey.Name });
        _audit.Record(security, "auth.passkey", "added", data: new Dictionary<string, string> { ["user"] = user.Id, ["attestation"] = format });
        return Result<WebPasskey>.Success(passkey);
    }

    private static JsonArray Descriptors(IEnumerable<WebPasskey> passkeys) =>
        new([.. passkeys.Select(p => (JsonNode)new JsonObject
        {
            ["type"] = "public-key",
            ["id"] = p.CredentialId,
            ["transports"] = new JsonArray([.. p.Transports.Select(t => (JsonNode)t)])
        })]);

    private static IReadOnlyList<string> Transports(JsonElement response) =>
        response.ValueKind == JsonValueKind.Object && response.TryGetProperty("transports", out var transports) && transports.ValueKind == JsonValueKind.Array
            ? [.. transports.EnumerateArray().Where(t => t.ValueKind == JsonValueKind.String).Select(t => t.GetString()!).Where(KnownTransports.Contains).Distinct().Take(6)]
            : [];
}
