using CodeLogic.Core.Configuration;

namespace CL.WebLogic.Configuration.Models;

/// <summary>TOTP two-factor authentication and recovery codes.</summary>
public sealed class TwoFactorConfig
{
    /// <summary>Issuer shown in authenticator apps (defaults to the site name).</summary>
    [ConfigField(Label = "Issuer", Group = "Two-factor", Order = 30)]
    public string Issuer { get; set; } = "";

    /// <summary>Recovery codes generated per user.</summary>
    [ConfigField(Label = "Recovery Codes", Min = 4, Max = 20, Group = "Two-factor", Order = 31)]
    public int RecoveryCodeCount { get; set; } = 10;

    /// <summary>Minutes a password-verified session may take to enter the second factor.</summary>
    [ConfigField(Label = "Second Factor Window (min)", Min = 1, Max = 60, Group = "Two-factor", Order = 32, Collapsed = true)]
    public int PendingMinutes { get; set; } = 10;
}
