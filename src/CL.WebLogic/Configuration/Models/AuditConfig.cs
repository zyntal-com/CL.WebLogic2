using CodeLogic.Core.Configuration;

namespace CL.WebLogic.Configuration.Models;

/// <summary>
/// The security audit trail. WebLogic only queues entries; the app's <c>IWebAuditSink</c> decides
/// whether and where they are stored.
/// </summary>
public sealed class AuditConfig
{
    /// <summary>Queue audit entries for the sink.</summary>
    [ConfigField(Label = "Enabled", Group = "Audit", Order = 120)]
    public bool Enabled { get; set; } = true;

    /// <summary>Also audit every request (not only security events).</summary>
    [ConfigField(Label = "Record All Requests", Group = "Audit", Order = 121, Collapsed = true)]
    public bool RecordAllRequests { get; set; }

    /// <summary>Key for hashing client IPs (HMAC-SHA256). Without it a random per-process key is used.</summary>
    [ConfigField(Label = "IP Hash Key", Description = "Secret key for HMAC-hashing client IPs in audit entries.", Secret = true, Group = "Audit", Order = 122, Collapsed = true)]
    public string IpHashKey { get; set; } = "";

    /// <summary>Queue capacity; when full the oldest entries are dropped (and counted).</summary>
    [ConfigField(Label = "Queue Capacity", Min = 100, Max = 1000000, Group = "Audit", Order = 123, Collapsed = true)]
    public int QueueCapacity { get; set; } = 10_000;
}
