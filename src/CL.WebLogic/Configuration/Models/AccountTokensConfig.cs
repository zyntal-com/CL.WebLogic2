using CodeLogic.Core.Configuration;

namespace CL.WebLogic.Configuration.Models;

/// <summary>Lifetimes and landing paths of e-mailed account tokens. Links use <c>PublicBaseUrl</c>.</summary>
public sealed class AccountTokensConfig
{
    /// <summary>Password reset token lifetime.</summary>
    [ConfigField(Label = "Reset (min)", Min = 5, Max = 1440, Group = "Account Tokens", Order = 40)]
    public int PasswordResetMinutes { get; set; } = 60;

    /// <summary>E-mail verification token lifetime.</summary>
    [ConfigField(Label = "Verify E-mail (h)", Min = 1, Max = 720, Group = "Account Tokens", Order = 41)]
    public int EmailVerificationHours { get; set; } = 48;

    /// <summary>Magic sign-in link lifetime.</summary>
    [ConfigField(Label = "Magic Link (min)", Min = 1, Max = 120, Group = "Account Tokens", Order = 42)]
    public int MagicLinkMinutes { get; set; } = 15;

    /// <summary>E-mail change confirmation lifetime.</summary>
    [ConfigField(Label = "E-mail Change (h)", Min = 1, Max = 168, Group = "Account Tokens", Order = 43)]
    public int EmailChangeHours { get; set; } = 24;

    /// <summary>Page that accepts <c>?token=</c> to set a new password.</summary>
    [ConfigField(Label = "Reset Path", Group = "Account Tokens", Order = 44, Collapsed = true)]
    public string PasswordResetPath { get; set; } = "/account/reset-password";

    /// <summary>Page that confirms an e-mail address.</summary>
    [ConfigField(Label = "Verify Path", Group = "Account Tokens", Order = 45, Collapsed = true)]
    public string EmailVerificationPath { get; set; } = "/account/verify-email";

    /// <summary>Page that completes a magic-link sign-in.</summary>
    [ConfigField(Label = "Magic Link Path", Group = "Account Tokens", Order = 46, Collapsed = true)]
    public string MagicLinkPath { get; set; } = "/account/magic-link";

    /// <summary>Page that confirms an e-mail change.</summary>
    [ConfigField(Label = "E-mail Change Path", Group = "Account Tokens", Order = 47, Collapsed = true)]
    public string EmailChangePath { get; set; } = "/account/confirm-email-change";
}
