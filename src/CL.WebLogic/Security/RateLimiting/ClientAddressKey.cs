using System.Net;
using System.Net.Sockets;

namespace CL.WebLogic.Security.RateLimiting;

/// <summary>
/// The key that per-client limits and throttles count against. IPv4 addresses count one by one;
/// IPv6 addresses count per network prefix (a /64 by default), because one subscriber usually
/// controls a whole /64 and could otherwise start fresh with every request.
/// </summary>
public static class ClientAddressKey
{
    /// <summary>The key for <paramref name="address"/> (IPv4-mapped IPv6 counts as IPv4).</summary>
    public static string For(IPAddress? address, int ipv6PrefixLength = 64)
    {
        if (address is null)
            return "unknown";
        if (address.IsIPv4MappedToIPv6)
            address = address.MapToIPv4();
        if (address.AddressFamily != AddressFamily.InterNetworkV6 || ipv6PrefixLength >= 128)
            return address.ToString();

        var prefix = Math.Clamp(ipv6PrefixLength, 16, 128);
        var bytes = address.GetAddressBytes();
        for (var bit = prefix; bit < 128; bit++)
            bytes[bit / 8] &= (byte)~(0x80 >> (bit % 8));
        return new IPAddress(bytes) + "/" + prefix.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }
}
