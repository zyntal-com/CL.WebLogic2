using CodeLogic.Core.Configuration;

namespace CL.WebLogic.Configuration.Models;

/// <summary>
/// <c>X-Forwarded-*</c> handling. Off by default and never trusts a proxy implicitly: headers are
/// honoured only from the listed proxy addresses / networks.
/// </summary>
public sealed class ForwardedHeadersConfig
{
    /// <summary>Honour forwarded headers from trusted proxies.</summary>
    [ConfigField(Label = "Enabled", Description = "Honour X-Forwarded-For/Proto/Host/Prefix from trusted proxies only.",
        Group = "Proxy", Order = 10)]
    public bool Enabled { get; set; }

    /// <summary>Proxy IP addresses allowed to set forwarded headers.</summary>
    [ConfigField(Label = "Trusted Proxies", Description = "Exact proxy IP addresses, e.g. 10.0.0.5.", Group = "Proxy", Order = 11)]
    public List<string> TrustedProxies { get; set; } = [];

    /// <summary>Proxy networks (CIDR) allowed to set forwarded headers.</summary>
    [ConfigField(Label = "Trusted Networks", Description = "Proxy networks in CIDR notation, e.g. 10.0.0.0/8. Never implied.", Group = "Proxy", Order = 12)]
    public List<string> TrustedNetworks { get; set; } = [];

    /// <summary>How many proxy hops to walk back through <c>X-Forwarded-For</c>.</summary>
    [ConfigField(Label = "Forward Limit", Min = 1, Max = 10, Group = "Proxy", Order = 13, Collapsed = true)]
    public int ForwardLimit { get; set; } = 1;

    /// <summary>Apply <c>X-Forwarded-Prefix</c> as the request path base.</summary>
    [ConfigField(Label = "Honour Prefix", Description = "Use X-Forwarded-Prefix as PathBase (app hosted under a sub-path).", Group = "Proxy", Order = 14, Collapsed = true)]
    public bool HonorPrefix { get; set; }
}
