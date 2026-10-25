namespace CL.WebLogic.Security.RateLimiting;

/// <summary>
/// Token-bucket storage. The default is in-memory (per node); a cluster registers a shared
/// implementation (e.g. Redis or a database) so limits hold across nodes.
/// </summary>
public interface IWebRateLimitStore
{
    /// <summary>Takes one token from the bucket <paramref name="key"/> governed by <paramref name="rule"/>.</summary>
    ValueTask<RateLimitDecision> TryAcquireAsync(string key, RateLimitRule rule, CancellationToken cancellationToken);
}
