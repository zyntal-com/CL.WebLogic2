using CodeLogic.Core.Configuration;

namespace CL.WebLogic.Configuration.Models;

/// <summary>Passkeys (WebAuthn): passwordless sign-in, a second factor and step-up.</summary>
public sealed class PasskeyConfig
{
    /// <summary>Allow passkey ceremonies (the app still needs a passkey store and routes).</summary>
    [ConfigField(Label = "Enabled", Group = "Passkeys", Order = 40)]
    public bool Enabled { get; set; } = true;

    /// <summary>The RP id passkeys are scoped to: the site's domain or a parent domain (defaults to the host of PublicBaseUrl).</summary>
    [ConfigField(Label = "Relying Party Id", Description = "Domain passkeys are bound to, e.g. example.com (covers its subdomains). Changing it orphans existing passkeys.",
        Group = "Passkeys", Order = 41)]
    public string RelyingPartyId { get; set; } = "";

    /// <summary>The name shown by the authenticator (defaults to the site name).</summary>
    [ConfigField(Label = "Relying Party Name", Group = "Passkeys", Order = 42)]
    public string RelyingPartyName { get; set; } = "";

    /// <summary>Page origins allowed to run ceremonies (defaults to the origin of PublicBaseUrl).</summary>
    [ConfigField(Label = "Allowed Origins", Description = "e.g. https://example.com and https://www.example.com.", Group = "Passkeys", Order = 43, Collapsed = true)]
    public List<string> Origins { get; set; } = [];

    /// <summary>Seconds the browser waits for the user.</summary>
    [ConfigField(Label = "Timeout (s)", Min = 30, Max = 600, Group = "Passkeys", Order = 44, Collapsed = true)]
    public int TimeoutSeconds { get; set; } = 300;

    /// <summary>
    /// Ask security keys without a PIN to be accepted as the second factor (user verification
    /// "preferred"). Passwordless sign-in and step-up always require user verification.
    /// </summary>
    [ConfigField(Label = "Second Factor Without PIN", Description = "Accept a touch-only security key after the password.", Group = "Passkeys", Order = 45, Collapsed = true)]
    public bool AllowUnverifiedSecondFactor { get; set; }

    /// <summary>Ask authenticators for attestation (Direct) and refuse formats WebLogic cannot verify.</summary>
    [ConfigField(Label = "Request Attestation", Description = "Off: no attestation (recommended). On: packed and fido-u2f are verified, others refused.",
        Group = "Passkeys", Order = 46, Collapsed = true)]
    public bool RequestAttestation { get; set; }

    /// <summary>Most passkeys per user.</summary>
    [ConfigField(Label = "Max Per User", Min = 1, Max = 100, Group = "Passkeys", Order = 47, Collapsed = true)]
    public int MaxPerUser { get; set; } = 20;
}
