namespace CL.WebLogic.Configuration.Models;

/// <summary>A named rate-limit policy: <see cref="Permits"/> per <see cref="WindowSeconds"/>, per partition.</summary>
public sealed class RateLimitPolicyConfig
{
    /// <summary>Policy name.</summary>
    public string Name { get; set; } = "";

    /// <summary>Requests allowed per window (also the burst size).</summary>
    public int Permits { get; set; } = 60;

    /// <summary>Window length in seconds; tokens refill evenly across it.</summary>
    public int WindowSeconds { get; set; } = 60;

    /// <summary>What a bucket is keyed by.</summary>
    public RateLimitPartition Partition { get; set; } = RateLimitPartition.Ip;
}
