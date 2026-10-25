using CodeLogic.Core.Configuration;

namespace CL.WebLogic.Configuration.Models;

/// <summary>
/// Running several nodes behind a load balancer. When enabled, startup fails while any state that
/// must be shared (sessions, accounts, one-time tokens, throttles, rate limits, realtime, the
/// DataProtection key ring) is still kept per process.
/// </summary>
public sealed class ClusterConfig
{
    /// <summary>This app runs as one of several nodes.</summary>
    [ConfigField(Label = "Enabled", Description = "Validate at startup that all shared state is stored outside the process.",
        RequiresRestart = true, Group = "Cluster", Order = 1)]
    public bool Enabled { get; set; }

    /// <summary>
    /// Contract names (e.g. <c>IWebRateLimitStore</c>) allowed to stay per node — a deliberate
    /// trade-off such as per-node rate limits behind sticky sessions. Each one is logged.
    /// </summary>
    [ConfigField(Label = "Allow Node-Local", Description = "Contracts that may keep per-node state (logged as warnings).",
        RequiresRestart = true, Group = "Cluster", Order = 2, Collapsed = true)]
    public List<string> AllowNodeLocal { get; set; } = [];
}
