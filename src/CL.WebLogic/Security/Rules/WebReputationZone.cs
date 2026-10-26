namespace CL.WebLogic.Security.Rules;

/// <summary>A DNS blocklist zone supplied by an <see cref="IWebReputationListProvider"/>.</summary>
/// <param name="Zone">The zone, e.g. <c>zen.spamhaus.org</c>.</param>
/// <param name="Ipv6">True for a zone that lists IPv6 addresses.</param>
/// <param name="Fallback">True to query it only when no primary zone lists the address.</param>
public sealed record WebReputationZone(string Zone, bool Ipv6 = false, bool Fallback = false);
