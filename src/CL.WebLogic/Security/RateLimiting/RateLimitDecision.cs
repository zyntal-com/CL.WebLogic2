namespace CL.WebLogic.Security.RateLimiting;

/// <summary>Outcome of a token request.</summary>
/// <param name="Allowed">True when a token was taken.</param>
/// <param name="Limit">Bucket capacity.</param>
/// <param name="Remaining">Whole tokens left.</param>
/// <param name="RetryAfter">When a token will be available (zero when allowed).</param>
/// <param name="Reset">Time until the bucket is full again.</param>
public readonly record struct RateLimitDecision(bool Allowed, int Limit, int Remaining, TimeSpan RetryAfter, TimeSpan Reset);
