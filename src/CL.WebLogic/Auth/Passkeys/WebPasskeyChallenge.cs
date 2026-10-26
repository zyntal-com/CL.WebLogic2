using System.Text.Json;

namespace CL.WebLogic.Auth.Passkeys;

/// <summary>
/// The start of a ceremony: <see cref="Options"/> go to <c>navigator.credentials.create/get</c>
/// (WebAuthn JSON form) and <see cref="State"/> comes back with the browser's answer. Return the
/// whole object as JSON; the client's <c>WebLogic.passkeys</c> helpers expect that shape.
/// </summary>
/// <param name="Options">PublicKeyCredentialCreationOptionsJSON or PublicKeyCredentialRequestOptionsJSON.</param>
/// <param name="State">Protected ceremony state (challenge, purpose, user, expiry); single use.</param>
public sealed record WebPasskeyChallenge(JsonElement Options, string State);
