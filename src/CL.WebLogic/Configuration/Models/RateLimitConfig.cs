using CodeLogic.Core.Configuration;

namespace CL.WebLogic.Configuration.Models;

/// <summary>Token-bucket rate limiting: a global per-IP limit plus named per-route policies.</summary>
public sealed class RateLimitConfig
{
    /// <summary>Apply the global per-IP limit.</summary>
    [ConfigField(Label = "Global Limit Enabled", Group = "Rate Limits", Order = 60)]
    public bool GlobalEnabled { get; set; } = true;

    /// <summary>Requests per minute per client IP across the whole site.</summary>
    [ConfigField(Label = "Global Requests / Minute", Min = 1, Max = 1000000, Group = "Rate Limits", Order = 61)]
    public int GlobalPermitsPerMinute { get; set; } = 1200;

    /// <summary>Extra burst allowance on top of the per-minute rate.</summary>
    [ConfigField(Label = "Global Burst", Min = 0, Max = 1000000, Group = "Rate Limits", Order = 62, Collapsed = true)]
    public int GlobalBurst { get; set; } = 200;

    /// <summary>Named policies referenced by routes with <c>.WithRateLimit("name")</c>.</summary>
    [ConfigField(Label = "Policies", Description = "Named policies used by routes (WithRateLimit).", Group = "Rate Limits", Order = 63)]
    public List<RateLimitPolicyConfig> Policies { get; set; } =
    [
        new() { Name = "login", Permits = 10, WindowSeconds = 300, Partition = RateLimitPartition.Ip },
        new() { Name = "api", Permits = 120, WindowSeconds = 60, Partition = RateLimitPartition.User },
        new() { Name = "forms", Permits = 30, WindowSeconds = 60, Partition = RateLimitPartition.Ip }
    ];

    /// <summary>Upper bound on tracked buckets; idle buckets are evicted first.</summary>
    [ConfigField(Label = "Max Tracked Keys", Min = 1000, Max = 10000000, Group = "Rate Limits", Order = 64, Collapsed = true)]
    public int MaxTrackedKeys { get; set; } = 100_000;

    /// <summary>IPv6 clients are counted per network of this prefix length (128 = per address).</summary>
    [ConfigField(Label = "IPv6 Prefix Length", Description = "IPv6 clients share limits per network of this size (one subscriber usually holds a /64).",
        Min = 32, Max = 128, Group = "Rate Limits", Order = 65, Collapsed = true)]
    public int Ipv6PrefixLength { get; set; } = 64;
}
