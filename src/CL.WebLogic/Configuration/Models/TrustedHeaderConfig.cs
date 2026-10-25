using CodeLogic.Core.Configuration;

namespace CL.WebLogic.Configuration.Models;

/// <summary>
/// Authentication by headers set by an authenticating reverse proxy (oauth2-proxy, Authelia, …).
/// Accepted only when the direct peer is a trusted proxy (<c>Security.ForwardedHeaders</c>)
/// <em>and</em> the shared secret header matches.
/// </summary>
public sealed class TrustedHeaderConfig
{
    /// <summary>Accept trusted proxy headers.</summary>
    [ConfigField(Label = "Enabled", Group = "Trusted Header", Order = 60)]
    public bool Enabled { get; set; }

    /// <summary>Header with the user id.</summary>
    [ConfigField(Label = "User Header", Group = "Trusted Header", Order = 61)]
    public string UserHeader { get; set; } = "X-Forwarded-User";

    /// <summary>Header with the user name (optional).</summary>
    [ConfigField(Label = "Name Header", Group = "Trusted Header", Order = 62, Collapsed = true)]
    public string NameHeader { get; set; } = "X-Forwarded-Preferred-Username";

    /// <summary>Header with the e-mail address (optional).</summary>
    [ConfigField(Label = "E-mail Header", Group = "Trusted Header", Order = 63, Collapsed = true)]
    public string EmailHeader { get; set; } = "X-Forwarded-Email";

    /// <summary>Header with comma-separated groups (optional).</summary>
    [ConfigField(Label = "Groups Header", Group = "Trusted Header", Order = 64, Collapsed = true)]
    public string GroupsHeader { get; set; } = "X-Forwarded-Groups";

    /// <summary>Header carrying the shared secret.</summary>
    [ConfigField(Label = "Secret Header", Group = "Trusted Header", Order = 65, Collapsed = true)]
    public string SecretHeader { get; set; } = "X-Proxy-Secret";

    /// <summary>The shared secret the proxy must send (required when enabled).</summary>
    [ConfigField(Label = "Secret", Secret = true, Group = "Trusted Header", Order = 66)]
    public string Secret { get; set; } = "";
}
