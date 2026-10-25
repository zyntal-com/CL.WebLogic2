namespace CL.WebLogic.Configuration.Models;

/// <summary>What a rate-limit bucket is keyed by.</summary>
public enum RateLimitPartition
{
    /// <summary>Client IP address.</summary>
    Ip,

    /// <summary>Signed-in user (falls back to the IP for anonymous requests).</summary>
    User,

    /// <summary>API key / bearer identity (falls back to the user, then the IP).</summary>
    ApiKey,

    /// <summary>The route itself (one bucket shared by everyone).</summary>
    Route,

    /// <summary>The tenant (falls back to the IP).</summary>
    Tenant
}
