using System.Net;
using CL.WebLogic.Security.Models;
using CL.WebLogic.Security.Services;

namespace CL.WebLogic.Security.Rules;

/// <summary>
/// Config and provider IP rules compiled for matching. Config rules get ids such as
/// <c>config.deny[0]</c>; provider rules keep their own. Immutable; rebuilt when the settings,
/// the provider rules or the next rule expiry change.
/// </summary>
public sealed class WebIpRuleSet
{
    private readonly (IPNetwork Network, string Id)[] _deny;
    private readonly (IPNetwork Network, string Id)[] _allow;
    private readonly (IPNetwork Network, string Id)[] _exempt;
    private readonly Dictionary<string, string> _denyCountries;
    private readonly Dictionary<string, string> _allowCountries;

    private WebIpRuleSet(List<(IPNetwork, string)> deny, List<(IPNetwork, string)> allow, List<(IPNetwork, string)> exempt,
        Dictionary<string, string> denyCountries, Dictionary<string, string> allowCountries, DateTimeOffset validUntil)
    {
        _deny = [.. deny];
        _allow = [.. allow];
        _exempt = [.. exempt];
        _denyCountries = denyCountries;
        _allowCountries = allowCountries;
        ValidUntil = validUntil;
    }

    /// <summary>When the earliest provider rule expires (the set must be rebuilt then).</summary>
    public DateTimeOffset ValidUntil { get; }

    /// <summary>True when there are country rules (a GeoIP lookup is needed).</summary>
    public bool HasCountryRules => _denyCountries.Count > 0 || _allowCountries.Count > 0;

    /// <summary>True when allow-list entries exist.</summary>
    public bool HasAllowList => _allow.Length > 0;

    /// <summary>The id of the first deny rule matching <paramref name="address"/>, or null.</summary>
    public string? MatchDeny(IPAddress? address) => Match(_deny, address);

    /// <summary>The id of the allow rule matching <paramref name="address"/>, or null.</summary>
    public string? MatchAllow(IPAddress? address) => Match(_allow, address);

    /// <summary>The id of the exempt rule matching <paramref name="address"/>, or null.</summary>
    public string? MatchExempt(IPAddress? address) => Match(_exempt, address);

    /// <summary>The id of the deny rule for <paramref name="country"/>, or null.</summary>
    public string? MatchDenyCountry(string country) => _denyCountries.GetValueOrDefault(country);

    /// <summary>True when <paramref name="country"/> passes the country allow-list (or there is none).</summary>
    public bool CountryAllowed(string country) => _allowCountries.Count == 0 || _allowCountries.ContainsKey(country);

    /// <summary>
    /// Compiles <paramref name="settings"/> rules plus <paramref name="providerRules"/> at
    /// <paramref name="now"/>. Invalid or expired provider rules are skipped and reported through
    /// <paramref name="invalid"/>.
    /// </summary>
    public static WebIpRuleSet Compile(WebSecuritySettings settings, IReadOnlyList<WebIpRule> providerRules, DateTimeOffset now, Action<WebIpRule>? invalid = null)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(providerRules);
        var deny = settings.DenyNetworks.Select((n, i) => (n, $"config.deny[{i}]")).ToList();
        var allow = settings.AllowNetworks.Select((n, i) => (n, $"config.allow[{i}]")).ToList();
        var exempt = new List<(IPNetwork, string)>();
        var denyCountries = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var allowCountries = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var country in settings.Config.IpRules.DenyCountries.Where(c => !string.IsNullOrWhiteSpace(c)))
            denyCountries.TryAdd(country.Trim(), "config.denyCountries");
        foreach (var country in settings.Config.IpRules.AllowCountries.Where(c => !string.IsNullOrWhiteSpace(c)))
            allowCountries.TryAdd(country.Trim(), "config.allowCountries");

        var validUntil = DateTimeOffset.MaxValue;
        foreach (var rule in providerRules)
        {
            if (rule.ExpiresAt is { } expires)
            {
                if (expires <= now)
                    continue;
                if (expires < validUntil)
                    validUntil = expires;
            }

            var hasCountry = !string.IsNullOrWhiteSpace(rule.Country);
            if (string.IsNullOrWhiteSpace(rule.Id) || hasCountry == !string.IsNullOrWhiteSpace(rule.Network)
                || (hasCountry && (rule.Action == WebIpRuleAction.Exempt || rule.Country!.Trim().Length != 2)))
            {
                invalid?.Invoke(rule);
                continue;
            }

            if (hasCountry)
            {
                (rule.Action == WebIpRuleAction.Deny ? denyCountries : allowCountries).TryAdd(rule.Country!.Trim(), rule.Id);
                continue;
            }
            if (!IpNetworkParser.TryParse(rule.Network, out var network))
            {
                invalid?.Invoke(rule);
                continue;
            }
            (rule.Action switch { WebIpRuleAction.Deny => deny, WebIpRuleAction.Allow => allow, _ => exempt }).Add((network, rule.Id));
        }

        return new WebIpRuleSet(deny, allow, exempt, denyCountries, allowCountries, validUntil);
    }

    private static string? Match((IPNetwork Network, string Id)[] rules, IPAddress? address)
    {
        if (address is null || rules.Length == 0)
            return null;
        address = IpNetworkParser.Normalize(address);
        foreach (var (network, id) in rules)
        {
            if (network.Contains(address))
                return id;
        }
        return null;
    }
}
