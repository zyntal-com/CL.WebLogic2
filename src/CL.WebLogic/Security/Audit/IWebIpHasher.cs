using System.Net;

namespace CL.WebLogic.Security.Audit;

/// <summary>Pseudonymises client IPs for audit logs and session binding.</summary>
public interface IWebIpHasher
{
    /// <summary>Hex HMAC of <paramref name="address"/>, or null.</summary>
    string? Hash(IPAddress? address);
}
