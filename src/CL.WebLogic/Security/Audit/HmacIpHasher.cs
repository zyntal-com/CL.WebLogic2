using System.Net;
using System.Security.Cryptography;
using System.Text;
using CL.WebLogic.Configuration.Services;
using CL.WebLogic.Hosting.Services;

namespace CL.WebLogic.Security.Audit;

/// <summary>
/// HMAC-SHA256 with <c>Security.Audit.IpHashKey</c>. Without a configured key a random key is
/// generated per process (hashes are then only comparable within one run) and a warning is logged.
/// </summary>
public sealed class HmacIpHasher : IWebIpHasher
{
    private readonly WebLogicConfigHolder _config;
    private readonly byte[] _fallbackKey = RandomNumberGenerator.GetBytes(32);

    /// <summary>Creates the hasher.</summary>
    public HmacIpHasher(WebLogicConfigHolder config, IWebLogicContext context)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        if (string.IsNullOrEmpty(config.Current.Security.Audit.IpHashKey))
            context.Logger.Warning("Security.Audit.IpHashKey is not set; IP hashes use a random per-process key.");
    }

    /// <inheritdoc/>
    public string? Hash(IPAddress? address)
    {
        if (address is null)
            return null;
        var configured = _config.Current.Security.Audit.IpHashKey;
        var key = string.IsNullOrEmpty(configured) ? _fallbackKey : Encoding.UTF8.GetBytes(configured);
        return Convert.ToHexStringLower(HMACSHA256.HashData(key, Encoding.ASCII.GetBytes(address.ToString())));
    }
}
