using System.Net;

namespace CL.WebLogic.Security.Services;

/// <summary>Parses an IP address (as a single-host network) or a CIDR network.</summary>
public static class IpNetworkParser
{
    /// <summary>Parses <c>10.0.0.0/8</c>, <c>2001:db8::/32</c> or a plain address such as <c>10.0.0.5</c>.</summary>
    public static bool TryParse(string? value, out IPNetwork network)
    {
        network = default;
        var text = value?.Trim();
        if (string.IsNullOrEmpty(text))
            return false;

        if (text.Contains('/'))
            return IPNetwork.TryParse(text, out network);

        if (!IPAddress.TryParse(text, out var address))
            return false;

        address = Normalize(address);
        network = new IPNetwork(address, address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork ? 32 : 128);
        return true;
    }

    /// <summary>Parses every entry, skipping invalid ones (config validation reports those).</summary>
    public static IReadOnlyList<IPNetwork> ParseAll(IEnumerable<string> values)
    {
        var networks = new List<IPNetwork>();
        foreach (var value in values)
        {
            if (TryParse(value, out var network))
                networks.Add(network);
        }
        return networks;
    }

    /// <summary>True when <paramref name="address"/> is inside any of <paramref name="networks"/>.</summary>
    public static bool ContainsAny(IReadOnlyList<IPNetwork> networks, IPAddress? address)
    {
        if (address is null || networks.Count == 0)
            return false;
        var normalized = Normalize(address);
        foreach (var network in networks)
        {
            if (network.Contains(normalized))
                return true;
        }
        return false;
    }

    /// <summary>Maps IPv4-mapped IPv6 addresses (<c>::ffff:1.2.3.4</c>) back to IPv4.</summary>
    public static IPAddress Normalize(IPAddress address) => address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address;
}
