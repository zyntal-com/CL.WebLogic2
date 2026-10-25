using CodeLogic.Core.Configuration;

namespace CL.WebLogic.Configuration.Models;

/// <summary>DNS blocklist (DNSBL) checks through CL.NetUtils.</summary>
public sealed class ReputationConfig
{
    /// <summary>Check client addresses against the CL.NetUtils DNSBL services.</summary>
    [ConfigField(Label = "Enabled", Description = "Check client IPs against DNS blocklists (CL.NetUtils).", Group = "Reputation", Order = 50)]
    public bool Enabled { get; set; }

    /// <summary>Blocking waits for the lookup (bounded by the timeout); Background never delays a request.</summary>
    [ConfigField(Label = "Mode", Description = "Blocking waits for the lookup; Background checks asynchronously and blocks from the next request.", Group = "Reputation", Order = 51)]
    public ReputationMode Mode { get; set; } = ReputationMode.Background;

    /// <summary>Lookup budget in Blocking mode.</summary>
    [ConfigField(Label = "Timeout (ms)", Min = 50, Max = 5000, Group = "Reputation", Order = 52, Collapsed = true)]
    public int TimeoutMilliseconds { get; set; } = 500;

    /// <summary>How long a verdict is cached per address.</summary>
    [ConfigField(Label = "Cache (minutes)", Min = 1, Max = 1440, Group = "Reputation", Order = 53, Collapsed = true)]
    public int CacheMinutes { get; set; } = 30;

    /// <summary>Maximum cached verdicts.</summary>
    [ConfigField(Label = "Cache Size", Min = 100, Max = 1000000, Group = "Reputation", Order = 54, Collapsed = true)]
    public int CacheSize { get; set; } = 50_000;
}
