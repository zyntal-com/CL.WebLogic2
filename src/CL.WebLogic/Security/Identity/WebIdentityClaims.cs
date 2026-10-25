namespace CL.WebLogic.Security.Identity;

/// <summary>Well-known keys of <see cref="WebIdentity.Claims"/>.</summary>
public static class WebIdentityClaims
{
    /// <summary>Id of the API key / token that authenticated the request (rate-limit partition).</summary>
    public const string KeyId = "key_id";

    /// <summary>Extra rate-limit policy applied to every request of this credential.</summary>
    public const string RateLimitPolicy = "rate_policy";

    /// <summary>E-mail address.</summary>
    public const string Email = "email";
}
