namespace CL.WebLogic.Security.Sessions;

/// <summary>
/// A stored session. Keyed by the SHA-256 hash of the cookie token — the raw token never reaches
/// the store, so a leaked store cannot be replayed as cookies.
/// </summary>
public sealed record WebSessionRecord
{
    /// <summary>Lower-case hex SHA-256 of the session token.</summary>
    public required string IdHash { get; init; }

    /// <summary>Creation time.</summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>Last activity (updated at most every touch interval).</summary>
    public required DateTimeOffset LastSeenAt { get; init; }

    /// <summary>Hard end of the session.</summary>
    public required DateTimeOffset AbsoluteExpiresAt { get; init; }

    /// <summary>Effective expiry (idle or absolute, whichever is first) — what stores sweep on.</summary>
    public required DateTimeOffset ExpiresAt { get; init; }

    /// <summary>Authentication progress.</summary>
    public WebSessionAuthStage AuthStage { get; init; }

    /// <summary>Signed-in (or pending) user id.</summary>
    public string? UserId { get; init; }

    /// <summary>User name.</summary>
    public string? UserName { get; init; }

    /// <summary>Groups captured at sign-in.</summary>
    public IReadOnlyList<string> Groups { get; init; } = [];

    /// <summary>Permissions captured at sign-in.</summary>
    public IReadOnlyList<string> Permissions { get; init; } = [];

    /// <summary>Authentication methods used.</summary>
    public IReadOnlyList<string> AuthenticationMethods { get; init; } = [];

    /// <summary>Tenant.</summary>
    public string? TenantId { get; init; }

    /// <summary>User security stamp at sign-in (lets the app end sessions after a password change).</summary>
    public string? SecurityStamp { get; init; }

    /// <summary>When the user last proved their credentials (sign-in or re-authentication).</summary>
    public DateTimeOffset? AuthenticatedAt { get; init; }

    /// <summary>"Remember me": longer timeouts and a cookie that survives a browser restart.</summary>
    public bool Persistent { get; init; }

    /// <summary>The browser's user agent when the session was created (at most 256 characters), for device lists.</summary>
    public string? UserAgent { get; init; }

    /// <summary>Per-session CSRF secret (base64url).</summary>
    public required string CsrfSecret { get; init; }

    /// <summary>HMAC of the client IP when sessions are IP-bound.</summary>
    public string? ClientIpHash { get; init; }

    /// <summary>App values.</summary>
    public IReadOnlyDictionary<string, string> Values { get; init; } = new Dictionary<string, string>();
}
