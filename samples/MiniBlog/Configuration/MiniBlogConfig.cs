using CodeLogic.Core.Configuration;

namespace MiniBlog.Configuration;

/// <summary>The blog's settings (<c>data/app/config.json</c>).</summary>
[ConfigSection("miniblog")]
public sealed class MiniBlogConfig : ConfigModelBase
{
    /// <summary>Shown under the site name.</summary>
    [ConfigField(Label = "Tagline", Group = "Blog", Order = 1)]
    public string Tagline { get; set; } = "Notes on building web apps with CodeLogic";

    /// <summary>Posts per page.</summary>
    [ConfigField(Label = "Posts per page", Group = "Blog", Order = 2, Min = 1, Max = 50)]
    public int PageSize { get; set; } = 5;

    /// <summary>The CL.MySQL2 connection.</summary>
    [ConfigField(Label = "Connection", Group = "Data", Order = 10, RequiresRestart = true)]
    public string ConnectionId { get; set; } = "Default";

    /// <summary>The editor account created on first start (password from DEMO_ADMIN_PASSWORD, or generated and logged once).</summary>
    [ConfigField(Label = "Admin User", Group = "Accounts", Order = 20)]
    public string AdminUserName { get; set; } = "admin";

    /// <summary>E-mail of the editor account (password resets go here).</summary>
    [ConfigField(Label = "Admin E-mail", Group = "Accounts", Order = 21)]
    public string AdminEmail { get; set; } = "editor@miniblog.local";

    /// <inheritdoc/>
    public override ConfigValidationResult Validate()
    {
        var errors = new List<string>(base.Validate().Errors);
        if (PageSize is < 1 or > 50)
            errors.Add("PageSize must be 1–50");
        return errors.Count == 0 ? ConfigValidationResult.Valid() : ConfigValidationResult.Invalid(errors);
    }
}
