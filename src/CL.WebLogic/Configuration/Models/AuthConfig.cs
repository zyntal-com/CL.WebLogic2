using CodeLogic.Core.Configuration;

namespace CL.WebLogic.Configuration.Models;

/// <summary>Sign-in, account lifecycle and API authentication.</summary>
public sealed class AuthConfig
{
    /// <summary>Refuse sign-in until the e-mail address is confirmed.</summary>
    [ConfigField(Label = "Require Confirmed E-mail", Group = "Auth", Order = 1)]
    public bool RequireConfirmedEmail { get; set; }

    /// <summary>Minutes between checks that a signed-in user's security stamp (password, 2FA…) is unchanged.</summary>
    [ConfigField(Label = "Stamp Check (min)", Description = "How often active sessions are re-validated against the user store (0 = never).",
        Min = 0, Max = 1440, Group = "Auth", Order = 2, Collapsed = true)]
    public int SecurityStampCheckMinutes { get; set; } = 5;

    /// <summary>Password hashing and policy.</summary>
    public PasswordConfig Passwords { get; set; } = new();

    /// <summary>Lockout and login throttling.</summary>
    public LockoutConfig Lockout { get; set; } = new();

    /// <summary>Two-factor authentication.</summary>
    public TwoFactorConfig TwoFactor { get; set; } = new();

    /// <summary>Account e-mail tokens (reset, verify, magic link, e-mail change).</summary>
    public AccountTokensConfig Tokens { get; set; } = new();

    /// <summary>API keys.</summary>
    public ApiKeyConfig ApiKeys { get; set; } = new();

    /// <summary>Authentication by a trusted proxy header (oauth2-proxy, …).</summary>
    public TrustedHeaderConfig TrustedHeader { get; set; } = new();

    /// <summary>Security notifications (e-mails).</summary>
    public NotificationsConfig Notifications { get; set; } = new();
}
