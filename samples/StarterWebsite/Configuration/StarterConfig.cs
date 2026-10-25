using CodeLogic.Core.Configuration;

namespace StarterWebsite.Configuration;

/// <summary>The starter application's settings (<c>data/app/config.json</c>).</summary>
[ConfigSection("starter")]
public sealed class StarterConfig : ConfigModelBase
{
    /// <summary>Anyone may create an account.</summary>
    [ConfigField(Label = "Open Registration", Group = "Accounts", Order = 1)]
    public bool AllowRegistration { get; set; } = true;

    /// <summary>The operator account created on first start (password from DEMO_ADMIN_PASSWORD, or generated and logged once).</summary>
    [ConfigField(Label = "Admin User", Group = "Accounts", Order = 2)]
    public string AdminUserName { get; set; } = "admin";

    /// <summary>E-mail address of the operator account.</summary>
    [ConfigField(Label = "Admin E-mail", Group = "Accounts", Order = 3)]
    public string AdminEmail { get; set; } = "admin@starter.local";

    /// <summary>Offer "Sign in through Steam" (OpenID 2.0; no API key needed).</summary>
    [ConfigField(Label = "Steam Sign-in", Group = "Accounts", Order = 4, RequiresRestart = true)]
    public bool SteamSignIn { get; set; }

    /// <summary>Longest message on the live wall.</summary>
    [ConfigField(Label = "Shout Length", Group = "Realtime", Order = 10, Min = 10, Max = 500)]
    public int MaxShoutLength { get; set; } = 140;

    /// <inheritdoc/>
    public override ConfigValidationResult Validate()
    {
        var errors = new List<string>(base.Validate().Errors);
        if (string.IsNullOrWhiteSpace(AdminUserName))
            errors.Add("AdminUserName is required");
        return errors.Count == 0 ? ConfigValidationResult.Valid() : ConfigValidationResult.Invalid(errors);
    }
}
