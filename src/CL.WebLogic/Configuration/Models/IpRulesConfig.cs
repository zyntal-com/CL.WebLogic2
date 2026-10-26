using CodeLogic.Core.Configuration;

namespace CL.WebLogic.Configuration.Models;

/// <summary>IP and country allow / deny rules, evaluated against the (proxy-resolved) client address.</summary>
public sealed class IpRulesConfig
{
    /// <summary>Only these addresses / networks may connect (empty = everyone).</summary>
    [ConfigField(Label = "Allow", Description = "IPs or CIDR networks allowed to connect. Empty = everyone.", Group = "IP Rules", Order = 40)]
    public List<string> Allow { get; set; } = [];

    /// <summary>Addresses / networks that are always refused.</summary>
    [ConfigField(Label = "Deny", Description = "IPs or CIDR networks that are always refused.", Group = "IP Rules", Order = 41)]
    public List<string> Deny { get; set; } = [];

    /// <summary>ISO country codes that are refused (needs CL.NetUtils with a GeoIP database).</summary>
    [ConfigField(Label = "Deny Countries", Description = "ISO 3166 country codes to refuse (CL.NetUtils GeoIP).", Group = "IP Rules", Order = 42, Collapsed = true)]
    public List<string> DenyCountries { get; set; } = [];

    /// <summary>When set, only these countries may connect (needs CL.NetUtils with a GeoIP database).</summary>
    [ConfigField(Label = "Allow Countries", Description = "ISO 3166 country codes allowed to connect. Empty = all.", Group = "IP Rules", Order = 43, Collapsed = true)]
    public List<string> AllowCountries { get; set; } = [];

    /// <summary>How long rules and zones from app providers are cached (invalidate to apply edits at once).</summary>
    [ConfigField(Label = "Provider Cache (seconds)", Description = "How long rules from IWebIpRuleProvider / IWebReputationListProvider are cached.", Min = 1, Max = 3600, Group = "IP Rules", Order = 44, Collapsed = true)]
    public int ProviderCacheSeconds { get; set; } = 60;
}
