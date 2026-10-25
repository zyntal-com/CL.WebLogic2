using System.Security.Claims;

namespace CL.WebLogic.Security.Identity;

/// <summary>
/// Who is making the request and how they proved it. Immutable; handlers create one and the
/// authentication stage installs it (and its <see cref="Principal"/> as <c>HttpContext.User</c>).
/// </summary>
public sealed class WebIdentity
{
    private ClaimsPrincipal? _principal;

    /// <summary>The unauthenticated identity.</summary>
    public static WebIdentity Anonymous { get; } = new() { Scheme = WebAuthenticationSchemes.None };

    /// <summary>True when authenticated.</summary>
    public bool IsAuthenticated => UserId is not null;

    /// <summary>Stable user id, or null for anonymous.</summary>
    public string? UserId { get; init; }

    /// <summary>Display / login name.</summary>
    public string? UserName { get; init; }

    /// <summary>How the identity was established (see <see cref="WebAuthenticationSchemes"/>).</summary>
    public required string Scheme { get; init; }

    /// <summary>
    /// True when the browser sends the credential automatically (session cookie, Windows auth,
    /// trusted proxy header) — such requests need CSRF protection. API keys and bearer tokens are not ambient.
    /// </summary>
    public bool IsAmbient { get; init; }

    /// <summary>Groups (roles) the user belongs to.</summary>
    public IReadOnlyList<string> Groups { get; init; } = [];

    /// <summary>Permissions granted directly with the identity; an <c>IWebPermissionResolver</c> may add more.</summary>
    public IReadOnlyList<string> Permissions { get; init; } = [];

    /// <summary>Scopes of an API key or token.</summary>
    public IReadOnlyList<string> Scopes { get; init; } = [];

    /// <summary>Tenant the identity belongs to.</summary>
    public string? TenantId { get; init; }

    /// <summary>Authentication methods used (<c>pwd</c>, <c>otp</c>, <c>apikey</c>…), RFC 8176 style.</summary>
    public IReadOnlyList<string> AuthenticationMethods { get; init; } = [];

    /// <summary>Additional claims.</summary>
    public IReadOnlyDictionary<string, string> Claims { get; init; } = new Dictionary<string, string>();

    /// <summary>The identity as a <see cref="ClaimsPrincipal"/> (role claims for groups, <c>permission</c> claims).</summary>
    public ClaimsPrincipal Principal => _principal ??= BuildPrincipal();

    private ClaimsPrincipal BuildPrincipal()
    {
        if (!IsAuthenticated)
            return new ClaimsPrincipal(new ClaimsIdentity());

        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, UserId!), new("wl:scheme", Scheme) };
        if (UserName is not null) claims.Add(new Claim(ClaimTypes.Name, UserName));
        if (TenantId is not null) claims.Add(new Claim("wl:tenant", TenantId));
        claims.AddRange(Groups.Select(g => new Claim(ClaimTypes.Role, g)));
        claims.AddRange(Permissions.Select(p => new Claim("permission", p)));
        claims.AddRange(Scopes.Select(s => new Claim("scope", s)));
        claims.AddRange(AuthenticationMethods.Select(m => new Claim("amr", m)));
        claims.AddRange(Claims.Select(c => new Claim(c.Key, c.Value)));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "WebLogic", ClaimTypes.Name, ClaimTypes.Role));
    }
}
