using CodeLogic.Core.Configuration;

namespace CL.WebLogic.Configuration.Models;

/// <summary>
/// Root configuration model for CL.WebLogic.
/// Serialized as <c>config.weblogic.json</c> in the library's config directory.
/// <para>
/// Services never capture this instance — they read the current value through
/// <see cref="Services.WebLogicConfigHolder"/>, which is swapped atomically when CodeLogic
/// applies a live config change.
/// </para>
/// </summary>
[ConfigSection("weblogic")]
public sealed class WebLogicConfig : ConfigModelBase
{
    /// <summary>Display name of the site, used in page titles, e-mails and the explorer.</summary>
    [ConfigField(Label = "Site Name", Description = "Displayed in page titles, e-mails and the explorer.",
        Placeholder = "My Site", Group = "General", Order = 0)]
    public string SiteName { get; set; } = "WebLogic Site";

    /// <summary>
    /// Canonical public base URL (scheme + host, optional path base), e.g. <c>https://example.com</c>.
    /// Used for absolute links, redirects and e-mails — never the request <c>Host</c> header.
    /// </summary>
    [ConfigField(Label = "Public Base URL", InputType = ConfigInputType.Url,
        Description = "Canonical scheme + host used for absolute links and redirects. Never derived from the Host header.",
        Placeholder = "https://example.com", Group = "General", Order = 1)]
    public string PublicBaseUrl { get; set; } = "";

    /// <summary>Theme location and static asset serving.</summary>
    public ThemeConfig Theme { get; set; } = new();

    /// <summary>Request-security pipeline.</summary>
    public SecurityConfig Security { get; set; } = new();

    /// <summary>Sign-in, account lifecycle and API authentication.</summary>
    public AuthConfig Auth { get; set; } = new();

    /// <summary>The client runtime.</summary>
    public ClientConfig Client { get; set; } = new();

    /// <summary>Forms.</summary>
    public FormsConfig Forms { get; set; } = new();

    /// <summary>Realtime hub.</summary>
    public RealtimeConfig Realtime { get; set; } = new();

    /// <summary>Output and fragment caching.</summary>
    public CacheConfig Cache { get; set; } = new();

    /// <summary>Response compression (off by default).</summary>
    public CompressionConfig Compression { get; set; } = new();

    /// <summary>Request cultures and localized texts (off by default).</summary>
    public LocalizationConfig Localization { get; set; } = new();

    /// <summary>API explorer.</summary>
    public ExplorerConfig Explorer { get; set; } = new();

    /// <summary>Running as several nodes (startup validation of shared state).</summary>
    public ClusterConfig Cluster { get; set; } = new();

    /// <inheritdoc/>
    public override ConfigValidationResult Validate()
    {
        var errors = new List<string>();
        errors.AddRange(base.Validate().Errors);

        if (string.IsNullOrWhiteSpace(SiteName))
            errors.Add("SiteName is required");

        if (!string.IsNullOrWhiteSpace(PublicBaseUrl)
            && (!Uri.TryCreate(PublicBaseUrl, UriKind.Absolute, out var uri)
                || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)))
            errors.Add("PublicBaseUrl must be an absolute http(s) URL");

        if (string.IsNullOrWhiteSpace(Theme.AssetsFolder) || Theme.AssetsFolder.Contains("..", StringComparison.Ordinal))
            errors.Add("Theme.AssetsFolder must be a folder name inside the theme");

        if (string.IsNullOrWhiteSpace(Theme.AssetsUrlPrefix) || !Theme.AssetsUrlPrefix.StartsWith('/'))
            errors.Add("Theme.AssetsUrlPrefix must start with '/'");

        if (Theme.Source == ThemeSourceKind.Git)
        {
            if (string.IsNullOrWhiteSpace(Theme.RepositoryUrl))
                errors.Add("Theme.RepositoryUrl is required when Theme.Source is Git");
            if (string.IsNullOrWhiteSpace(Theme.RepositoryId) || !Theme.RepositoryId.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.') || Theme.RepositoryId.Contains(".."))
                errors.Add("Theme.RepositoryId may only contain letters, digits, '-', '_' and '.'");
            if (Theme.ThemeSubPath.Contains("..", StringComparison.Ordinal))
                errors.Add("Theme.ThemeSubPath must stay inside the repository");
        }

        if (string.IsNullOrWhiteSpace(Security.LoginPath) || !Security.LoginPath.StartsWith('/') || Security.LoginPath.StartsWith("//", StringComparison.Ordinal))
            errors.Add("Security.LoginPath must be a local path");

        foreach (var cidr in Security.ForwardedHeaders.TrustedNetworks.Concat(Security.IpRules.Allow).Concat(Security.IpRules.Deny))
        {
            if (!global::CL.WebLogic.Security.Services.IpNetworkParser.TryParse(cidr, out _))
                errors.Add($"'{cidr}' is not a valid IP address or CIDR network");
        }

        foreach (var proxy in Security.ForwardedHeaders.TrustedProxies)
        {
            if (!System.Net.IPAddress.TryParse(proxy, out _))
                errors.Add($"'{proxy}' is not a valid proxy IP address");
        }

        foreach (var policy in Security.RateLimits.Policies)
        {
            if (string.IsNullOrWhiteSpace(policy.Name) || policy.Permits < 1 || policy.WindowSeconds < 1)
                errors.Add($"Rate limit policy '{policy.Name}' needs a name, Permits >= 1 and WindowSeconds >= 1");
        }

        foreach (var origin in Realtime.AllowedOrigins)
        {
            if (!Uri.TryCreate(origin, UriKind.Absolute, out var originUri) || originUri.Scheme is not ("http" or "https") || originUri.AbsolutePath != "/")
                errors.Add($"Realtime.AllowedOrigins: '{origin}' must be an origin such as https://app.example.com");
        }

        if (Realtime.ClientTransport is not ("sse" or "ws"))
            errors.Add("Realtime.ClientTransport must be sse or ws");

        if (Forms.Renderer is not ("bootstrap5" or "plain"))
            errors.Add("Forms.Renderer must be bootstrap5 or plain");

        if (Client.UiAdapter is not ("plain" or "bootstrap5" or "none"))
            errors.Add("Client.UiAdapter must be plain, bootstrap5 or none");

        if (Client.Format is not ("module" or "classic"))
            errors.Add("Client.Format must be module or classic");

        if (Auth.TrustedHeader.Enabled && string.IsNullOrWhiteSpace(Auth.TrustedHeader.Secret))
            errors.Add("Auth.TrustedHeader.Secret is required when trusted-header authentication is enabled");

        if (Auth.Passwords.MinimumLength > Auth.Passwords.MaximumLength)
            errors.Add("Auth.Passwords.MinimumLength cannot exceed MaximumLength");

        foreach (var origin in Auth.Passkeys.Origins)
        {
            if (!Uri.TryCreate(origin, UriKind.Absolute, out var originUri) || originUri.AbsolutePath != "/" || !string.IsNullOrEmpty(originUri.Query)
                || !(originUri.Scheme == Uri.UriSchemeHttps || (originUri.Scheme == Uri.UriSchemeHttp && originUri.IsLoopback)))
                errors.Add($"Auth.Passkeys.Origins: '{origin}' must be an https origin (http only for localhost)");
        }
        if (Auth.Passkeys.RelyingPartyId.Length > 0
            && (Uri.CheckHostName(Auth.Passkeys.RelyingPartyId) != UriHostNameType.Dns || Auth.Passkeys.RelyingPartyId.Contains(':', StringComparison.Ordinal)))
            errors.Add("Auth.Passkeys.RelyingPartyId must be a domain name (no scheme, port or IP address)");

        if (Security.Hosts.RedirectToCanonical && string.IsNullOrWhiteSpace(PublicBaseUrl))
            errors.Add("Security.Hosts.RedirectToCanonical requires PublicBaseUrl");

        foreach (var mimeType in Compression.MimeTypes)
        {
            if (string.IsNullOrWhiteSpace(mimeType) || mimeType.Count(c => c == '/') != 1 || mimeType.Any(char.IsWhiteSpace))
                errors.Add($"Compression.MimeTypes: '{mimeType}' must be a content type such as application/manifest+json");
        }

        if (Theme.Source == ThemeSourceKind.Storage && string.IsNullOrWhiteSpace(Theme.StorageConnectionId))
            errors.Add("Theme.StorageConnectionId is required when Theme.Source is Storage");

        errors.AddRange(Localization.Validate());

        return errors.Count == 0 ? ConfigValidationResult.Valid() : ConfigValidationResult.Invalid(errors);
    }
}
