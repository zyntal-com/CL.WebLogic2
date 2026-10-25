using CL.WebLogic.Configuration.Models;

namespace CL.WebLogic.Security.RateLimiting;

/// <summary>A token bucket: <see cref="Capacity"/> tokens, refilled at <see cref="RefillPerSecond"/>.</summary>
public sealed record RateLimitRule(string Name, int Capacity, double RefillPerSecond, RateLimitPartition Partition)
{
    /// <summary>Builds a rule from a named policy: <c>Permits</c> per <c>WindowSeconds</c>.</summary>
    public static RateLimitRule From(RateLimitPolicyConfig policy) =>
        new(policy.Name, Math.Max(1, policy.Permits), Math.Max(1, policy.Permits) / (double)Math.Max(1, policy.WindowSeconds), policy.Partition);
}
