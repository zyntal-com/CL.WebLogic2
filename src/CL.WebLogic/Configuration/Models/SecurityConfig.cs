using CodeLogic.Core.Configuration;

namespace CL.WebLogic.Configuration.Models;

/// <summary>The request-security pipeline: stages, sessions, CSRF, response headers and audit.</summary>
public sealed class SecurityConfig
{
    /// <summary>Stage names to switch off (e.g. <c>reputation</c>). Disabling a stage is logged at startup.</summary>
    [ConfigField(Label = "Disabled Stages", Description = "Names of security stages to skip. Every disabled stage is logged at startup.",
        RequiresRestart = true, Group = "Security", Order = 1, Collapsed = true)]
    public List<string> DisabledStages { get; set; } = [];

    /// <summary>Where pages send unauthenticated visitors (with a safe <c>returnUrl</c>).</summary>
    [ConfigField(Label = "Login Path", Description = "Local path unauthenticated page requests are redirected to.",
        Placeholder = "/login", Group = "Security", Order = 2)]
    public string LoginPath { get; set; } = "/login";

    /// <summary>
    /// Allows the in-memory development stores (sessions, …) outside Development. Off by default:
    /// production must register real stores.
    /// </summary>
    [ConfigField(Label = "Allow Development Stores", Description = "Permit in-memory development stores outside the Development environment.",
        RequiresRestart = true, Group = "Security", Order = 3, Collapsed = true)]
    public bool AllowDevelopmentStores { get; set; }

    /// <summary>Paths that skip host filtering and HTTPS redirection (load-balancer probes).</summary>
    [ConfigField(Label = "Probe Paths", Description = "Path prefixes exempt from host filtering and HTTPS redirection.",
        Group = "Security", Order = 4, Collapsed = true)]
    public List<string> ProbePaths { get; set; } = ["/_weblogic/health"];

    /// <summary>Reverse-proxy header handling.</summary>
    public ForwardedHeadersConfig ForwardedHeaders { get; set; } = new();

    /// <summary>Host filtering and canonical host.</summary>
    public HostsConfig Hosts { get; set; } = new();

    /// <summary>HTTPS redirection and HSTS.</summary>
    public HttpsConfig Https { get; set; } = new();

    /// <summary>IP allow / deny rules.</summary>
    public IpRulesConfig IpRules { get; set; } = new();

    /// <summary>DNS blocklist checks.</summary>
    public ReputationConfig Reputation { get; set; } = new();

    /// <summary>Rate limiting.</summary>
    public RateLimitConfig RateLimits { get; set; } = new();

    /// <summary>Request size limits.</summary>
    public RequestLimitsConfig Limits { get; set; } = new();

    /// <summary>Sessions.</summary>
    public SessionConfig Session { get; set; } = new();

    /// <summary>Cross-site request forgery protection.</summary>
    public CsrfConfig Csrf { get; set; } = new();

    /// <summary>Security response headers (CSP, HSTS, …).</summary>
    public ResponseHeadersConfig Headers { get; set; } = new();

    /// <summary>Security audit trail.</summary>
    public AuditConfig Audit { get; set; } = new();
}
