using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using CL.WebLogic.Auth.Events;
using CL.WebLogic.Auth.Passkeys.Cbor;
using CL.WebLogic.Auth.Users;
using CL.WebLogic.Security.Pipeline;
using CodeLogic.Core.Results;

namespace CL.WebLogic.Auth.Passkeys;

public sealed partial class WebPasskeyService
{
    /// <summary>
    /// Starts an assertion. <see cref="WebPasskeyPurpose.SignIn"/> needs no user (the browser offers
    /// the user's discoverable passkeys); the other purposes list <paramref name="user"/>'s passkeys
    /// and fail when there are none.
    /// </summary>
    public async Task<Result<WebPasskeyChallenge>> BeginAssertionAsync(WebSecurityContext security, WebPasskeyPurpose purpose, WebUser? user, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(security);
        if (!Settings.Enabled)
            return Result<WebPasskeyChallenge>.Failure(Error.Validation("weblogic.passkeys.disabled", "Passkeys are turned off."));
        IReadOnlyList<WebPasskey> passkeys = [];
        if (purpose != WebPasskeyPurpose.SignIn)
        {
            if (user is null)
                return Result<WebPasskeyChallenge>.Failure(Error.Unauthorized("weblogic.auth.not_signed_in", "Sign in first."));
            passkeys = await _store.ListAsync(user.Id, cancellationToken).ConfigureAwait(false);
            if (passkeys.Count == 0)
                return Result<WebPasskeyChallenge>.Failure(Error.Validation("weblogic.passkeys.none", "There are no passkeys on this account."));
        }

        var requireVerification = purpose != WebPasskeyPurpose.SecondFactor || !Settings.AllowUnverifiedSecondFactor;
        var rpId = RelyingPartyId(security);
        var (challenge, state) = NewState("get:" + purpose, rpId, purpose == WebPasskeyPurpose.SignIn ? null : user!.Id, null, requireVerification);
        var options = new JsonObject
        {
            ["challenge"] = challenge,
            ["rpId"] = rpId,
            ["timeout"] = Settings.TimeoutSeconds * 1000,
            ["userVerification"] = requireVerification ? "required" : "preferred",
            ["allowCredentials"] = Descriptors(passkeys)
        };
        return Result<WebPasskeyChallenge>.Success(new WebPasskeyChallenge(JsonDocument.Parse(options.ToJsonString()).RootElement.Clone(), state));
    }

    /// <summary>
    /// Verifies an assertion for <paramref name="purpose"/>: state, credential, client data, RP id,
    /// flags, signature and counter. A counter that goes backwards refuses the sign-in
    /// (<see cref="PasskeyCloneSuspectedEvent"/>). Account rules are left to the caller.
    /// </summary>
    public async Task<Result<WebPasskeyAssertion>> VerifyAssertionAsync(WebSecurityContext security, WebPasskeyPurpose purpose, WebPasskeyRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(security);
        ArgumentNullException.ThrowIfNull(request);
        var state = await SpendStateAsync(request.State, "get:" + purpose, cancellationToken).ConfigureAwait(false);
        if (state is null)
            return Fail<WebPasskeyAssertion>(security, "assertion", "expired, reused or foreign state", "The request expired; try again.");

        var credential = request.Credential;
        var response = credential.ValueKind == JsonValueKind.Object && credential.TryGetProperty("response", out var r) ? r : default;
        var id = Text(credential, "id");
        var clientDataBytes = Decode(Text(response, "clientDataJSON"));
        var authenticatorBytes = Decode(Text(response, "authenticatorData"));
        var signature = Decode(Text(response, "signature"));
        var userHandle = Text(response, "userHandle");
        if (Text(credential, "type") != "public-key" || id is null || Text(credential, "rawId") is { } rawId && rawId != id
            || clientDataBytes is null || authenticatorBytes is null || signature is null)
            return Fail<WebPasskeyAssertion>(security, "assertion", "malformed credential");

        var passkey = await _store.FindAsync(id, cancellationToken).ConfigureAwait(false);
        if (passkey is null)
            return Fail<WebPasskeyAssertion>(security, "assertion", "unknown credential", "This passkey is not registered here.");
        if (state.UserId is not null && passkey.UserId != state.UserId)
            return Fail<WebPasskeyAssertion>(security, "assertion", "passkey of another user");
        if (string.IsNullOrEmpty(userHandle) ? purpose == WebPasskeyPurpose.SignIn
                : !CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(userHandle), Encoding.ASCII.GetBytes(passkey.UserHandle)))
            return Fail<WebPasskeyAssertion>(security, "assertion", "user handle does not match");

        var clientData = WebClientData.TryParse(clientDataBytes);
        if (CheckClientData(clientData, "webauthn.get", state, security) is { } clientProblem)
            return Fail<WebPasskeyAssertion>(security, "assertion", clientProblem);

        WebAuthenticatorData data;
        WebCoseKey key;
        try
        {
            data = WebAuthenticatorData.Parse(authenticatorBytes);
            key = WebCoseKey.Parse(passkey.PublicKey);
        }
        catch (WebCborException ex)
        {
            return Fail<WebPasskeyAssertion>(security, "assertion", "malformed authenticator data: " + ex.Message);
        }
        if (CheckAuthenticatorData(data, state) is { } dataProblem)
            return Fail<WebPasskeyAssertion>(security, "assertion", dataProblem);
        if (!key.Verify([.. data.Raw, .. clientData!.Hash], signature))
            return Fail<WebPasskeyAssertion>(security, "assertion", "bad signature");

        // Counters only move forward; both zero means the authenticator keeps none (synced passkeys).
        if ((data.SignCount != 0 || passkey.SignCount != 0) && data.SignCount <= passkey.SignCount)
        {
            _context.Events.Publish(new PasskeyCloneSuspectedEvent(passkey.UserId, passkey.CredentialId, passkey.SignCount, data.SignCount));
            return Fail<WebPasskeyAssertion>(security, "assertion", $"signature counter went from {passkey.SignCount} to {data.SignCount} (cloned authenticator?)");
        }
        if (!await _store.TryRecordUseAsync(passkey.CredentialId, passkey.SignCount, data.SignCount, data.Has(WebAuthenticatorBits.BackedUp), _time.GetUtcNow(), cancellationToken).ConfigureAwait(false))
            return Fail<WebPasskeyAssertion>(security, "assertion", "passkey used concurrently");

        var user = await _users.FindByIdAsync(passkey.UserId, cancellationToken).ConfigureAwait(false);
        if (user is null)
            return Fail<WebPasskeyAssertion>(security, "assertion", "the passkey's user no longer exists");
        return Result<WebPasskeyAssertion>.Success(new WebPasskeyAssertion(user, passkey, data.Has(WebAuthenticatorBits.UserVerified)));
    }
}
