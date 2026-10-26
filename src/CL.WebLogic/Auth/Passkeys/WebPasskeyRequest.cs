using System.Text.Json;

namespace CL.WebLogic.Auth.Passkeys;

/// <summary>What the client posts to finish a ceremony (<c>WebLogic.passkeys</c> sends this shape).</summary>
public sealed record WebPasskeyRequest
{
    /// <summary>The browser's answer: <c>PublicKeyCredential.toJSON()</c>.</summary>
    public JsonElement Credential { get; init; }

    /// <summary>The <see cref="WebPasskeyChallenge.State"/> from the start of the ceremony.</summary>
    public string State { get; init; } = "";

    /// <summary>A name for a new passkey (registration).</summary>
    public string? Name { get; init; }

    /// <summary>"Remember me" (sign-in).</summary>
    public bool Remember { get; init; }
}
