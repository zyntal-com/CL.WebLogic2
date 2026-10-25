using CodeLogic.Core.Configuration;

namespace CL.WebLogic.Configuration.Models;

/// <summary>HTTPS redirection and Strict-Transport-Security.</summary>
public sealed class HttpsConfig
{
    /// <summary>Redirect plain HTTP to HTTPS (308) on the canonical host — never on the request's Host header.</summary>
    [ConfigField(Label = "Enforce HTTPS", Description = "Redirect HTTP to HTTPS on the PublicBaseUrl host.", Group = "HTTPS", Order = 30)]
    public bool Enforce { get; set; }

    /// <summary>Send <c>Strict-Transport-Security</c> on HTTPS responses.</summary>
    [ConfigField(Label = "HSTS", Description = "Send Strict-Transport-Security on HTTPS responses.", Group = "HTTPS", Order = 31)]
    public bool Hsts { get; set; } = true;

    /// <summary>HSTS max-age in days.</summary>
    [ConfigField(Label = "HSTS Max Age (days)", Min = 1, Max = 730, Group = "HTTPS", Order = 32, Collapsed = true)]
    public int HstsMaxAgeDays { get; set; } = 180;

    /// <summary>Add <c>includeSubDomains</c> to HSTS.</summary>
    [ConfigField(Label = "HSTS Sub-domains", Group = "HTTPS", Order = 33, Collapsed = true)]
    public bool HstsIncludeSubDomains { get; set; }

    /// <summary>Add <c>preload</c> to HSTS (only after submitting the domain to the preload list).</summary>
    [ConfigField(Label = "HSTS Preload", Group = "HTTPS", Order = 34, Collapsed = true)]
    public bool HstsPreload { get; set; }
}
