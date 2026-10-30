using System.Net;
using CL.WebLogic.Configuration.Models;
using CL.WebLogic.Security.RateLimiting;
using CL.WebLogic.Security.Services;

namespace CL.WebLogic.Security.Models;

/// <summary>
/// Security config compiled once per config revision: parsed networks, rate rules, the CSP
/// template and the HSTS value. Services read it per request and never keep it.
/// </summary>
public sealed class WebSecuritySettings
{
    private WebSecuritySettings(WebLogicConfig config, long revision)
    {
        var security = config.Security;
        Config = security;
        Revision = revision;
        PublicBaseUri = Uri.TryCreate(config.PublicBaseUrl, UriKind.Absolute, out var uri) ? uri : null;
        DisabledStages = new HashSet<string>(security.DisabledStages, StringComparer.OrdinalIgnoreCase);
        TrustedProxies = IpNetworkParser.ParseAll(security.ForwardedHeaders.TrustedProxies.Concat(security.ForwardedHeaders.TrustedNetworks));
        AllowNetworks = IpNetworkParser.ParseAll(security.IpRules.Allow);
        DenyNetworks = IpNetworkParser.ParseAll(security.IpRules.Deny);
        AllowedHosts = new HostMatcher(security.Hosts.AllowedHosts);
        TrustedOrigins = new HashSet<string>(security.Csrf.TrustedOrigins.Select(NormalizeOrigin).Where(o => o.Length > 0), StringComparer.OrdinalIgnoreCase);
        GlobalRateLimit = new RateLimitRule("global", security.RateLimits.GlobalPermitsPerMinute + security.RateLimits.GlobalBurst,
            security.RateLimits.GlobalPermitsPerMinute / 60d, RateLimitPartition.Ip);
        RateLimitPolicies = security.RateLimits.Policies
            .Where(p => !string.IsNullOrWhiteSpace(p.Name))
            .GroupBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => RateLimitRule.From(g.Last()), StringComparer.OrdinalIgnoreCase);
        ContentSecurityPolicy = CspBuilder.Build(security.Headers);
        StrictTransportSecurity = BuildHsts(security.Https);
    }

    /// <summary>The source config section.</summary>
    public SecurityConfig Config { get; }

    /// <summary>Config revision these settings were compiled from.</summary>
    public long Revision { get; }

    /// <summary><c>PublicBaseUrl</c>, when configured — the only source for absolute redirects.</summary>
    public Uri? PublicBaseUri { get; }

    /// <summary>Disabled stage names.</summary>
    public IReadOnlySet<string> DisabledStages { get; }

    /// <summary>Proxies allowed to set forwarded headers.</summary>
    public IReadOnlyList<IPNetwork> TrustedProxies { get; }

    /// <summary>Allow list (empty = everyone).</summary>
    public IReadOnlyList<IPNetwork> AllowNetworks { get; }

    /// <summary>Deny list.</summary>
    public IReadOnlyList<IPNetwork> DenyNetworks { get; }

    /// <summary>Accepted hosts.</summary>
    public HostMatcher AllowedHosts { get; }

    /// <summary>Extra origins allowed to send unsafe requests.</summary>
    public IReadOnlySet<string> TrustedOrigins { get; }

    /// <summary>The global per-IP rule.</summary>
    public RateLimitRule GlobalRateLimit { get; }

    /// <summary>Named route policies.</summary>
    public IReadOnlyDictionary<string, RateLimitRule> RateLimitPolicies { get; }

    /// <summary>CSP with a <c>{nonce}</c> placeholder, or null when disabled.</summary>
    public string? ContentSecurityPolicy { get; }

    /// <summary>HSTS header value, or null when disabled.</summary>
    public string? StrictTransportSecurity { get; }

    /// <summary>
    /// True when the path is a probe path or below one (exempt from host filtering and HTTPS
    /// redirection). Matches whole segments: <c>/health</c> covers <c>/health/live</c>, not <c>/healthcare</c>.
    /// </summary>
    public bool IsProbePath(string? path) =>
        path is not null && Config.ProbePaths.Any(p =>
        {
            var probe = p.TrimEnd('/');
            return path.StartsWith(probe, StringComparison.OrdinalIgnoreCase)
                && (path.Length == probe.Length || path[probe.Length] == '/');
        });

    /// <summary>Compiles <paramref name="config"/>.</summary>
    public static WebSecuritySettings Compile(WebLogicConfig config, long revision) => new(config, revision);

    /// <summary><c>scheme://host[:port]</c> in lower case, or empty when not an absolute http(s) URL.</summary>
    public static string NormalizeOrigin(string? origin) =>
        Uri.TryCreate(origin?.Trim(), UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            ? uri.GetLeftPart(UriPartial.Authority).ToLowerInvariant()
            : string.Empty;

    private static string? BuildHsts(HttpsConfig https)
    {
        if (!https.Hsts)
            return null;
        var value = "max-age=" + (https.HstsMaxAgeDays * 86400L).ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (https.HstsIncludeSubDomains) value += "; includeSubDomains";
        if (https.HstsPreload) value += "; preload";
        return value;
    }
}
