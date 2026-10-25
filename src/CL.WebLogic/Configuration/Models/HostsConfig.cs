using CodeLogic.Core.Configuration;

namespace CL.WebLogic.Configuration.Models;

/// <summary>Accepted Host headers and the canonical host.</summary>
public sealed class HostsConfig
{
    /// <summary>Accepted hosts (<c>example.com</c>, <c>*.example.com</c>). Empty accepts any host.</summary>
    [ConfigField(Label = "Allowed Hosts", Description = "Accepted Host headers; '*.example.com' matches sub-domains. Empty = any.", Group = "Hosts", Order = 20)]
    public List<string> AllowedHosts { get; set; } = [];

    /// <summary>Redirect other accepted hosts to the host of <c>PublicBaseUrl</c> (301).</summary>
    [ConfigField(Label = "Redirect To Canonical", Description = "Permanently redirect requests for other hosts to the PublicBaseUrl host.", Group = "Hosts", Order = 21)]
    public bool RedirectToCanonical { get; set; }
}
