using System.Net;
using CL.WebLogic.Routing.Models;
using CL.WebLogic.Security.Identity;
using CL.WebLogic.Security.Models;
using Microsoft.AspNetCore.Http;

namespace CL.WebLogic.Security.Pipeline;

/// <summary>
/// Per-request security state shared by the stages, the handlers and the app. Stored as an
/// HttpContext feature; read it with <see cref="Get"/> or <c>request.Security</c>.
/// </summary>
public sealed class WebSecurityContext
{
    private Dictionary<string, string>? _headerOverrides;

    /// <summary>Creates the context.</summary>
    public WebSecurityContext(HttpContext httpContext, WebSecuritySettings settings)
    {
        HttpContext = httpContext ?? throw new ArgumentNullException(nameof(httpContext));
        Settings = settings ?? throw new ArgumentNullException(nameof(settings));
        ClientIp = httpContext.Connection.RemoteIpAddress is { } ip ? Services.IpNetworkParser.Normalize(ip) : null;
        PeerIp = ClientIp;
    }

    /// <summary>The request.</summary>
    public HttpContext HttpContext { get; }

    /// <summary>Compiled settings for this request.</summary>
    public WebSecuritySettings Settings { get; }

    /// <summary>The address of the direct peer (the proxy, when behind one), before forwarded headers.</summary>
    public IPAddress? PeerIp { get; }

    /// <summary>True when the direct peer is a configured trusted proxy.</summary>
    public bool PeerIsTrustedProxy => PeerIp is not null && Services.IpNetworkParser.ContainsAny(Settings.TrustedProxies, PeerIp);

    /// <summary>Client address (after trusted forwarded headers).</summary>
    public IPAddress? ClientIp { get; internal set; }

    /// <summary>The authenticated identity, or <see cref="WebIdentity.Anonymous"/>.</summary>
    public WebIdentity Identity { get; internal set; } = WebIdentity.Anonymous;

    /// <summary>The matched WebLogic route (endpoint phase), or null for other endpoints.</summary>
    public WebRouteMetadata? Route { get; internal set; }

    /// <summary>Tenant resolved for this request, if any.</summary>
    public string? TenantId { get; internal set; }

    /// <summary>True when an exempt IP rule matched: country rules and the reputation check are skipped.</summary>
    public bool IpExempt { get; internal set; }

    /// <summary>True once a CSRF token was handed out (the response must not be cached).</summary>
    public bool CsrfTokenIssued { get; internal set; }

    /// <summary>Security headers this response explicitly overrides (see <see cref="OverrideHeader"/>).</summary>
    public IReadOnlyDictionary<string, string> HeaderOverrides =>
        _headerOverrides ?? (IReadOnlyDictionary<string, string>)new Dictionary<string, string>();

    /// <summary>
    /// Replaces a security header (CSP, frame options, CORP…) for this response only — e.g. an
    /// embeddable widget page. The only way to weaken a security header; every use is logged.
    /// </summary>
    public void OverrideHeader(string name, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        (_headerOverrides ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase))[name] = value ?? string.Empty;
    }

    /// <summary>The security context of <paramref name="httpContext"/>, or null when the pipeline did not run.</summary>
    public static WebSecurityContext? Get(HttpContext httpContext) => httpContext?.Features.Get<WebSecurityContext>();
}
