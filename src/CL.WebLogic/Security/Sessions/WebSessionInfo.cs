namespace CL.WebLogic.Security.Sessions;

/// <summary>One of the signed-in user's sessions, for a "signed-in devices" page. Holds no secrets.</summary>
public sealed record WebSessionInfo
{
    /// <summary>The session's id hash: pass it to <c>request.Auth.RevokeSessionAsync</c>.</summary>
    public required string IdHash { get; init; }

    /// <summary>Creation time.</summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>Last activity (updated at most every touch interval).</summary>
    public required DateTimeOffset LastSeenAt { get; init; }

    /// <summary>When the user last proved their credentials in that session.</summary>
    public DateTimeOffset? AuthenticatedAt { get; init; }

    /// <summary>The browser's user agent, if known.</summary>
    public string? UserAgent { get; init; }

    /// <summary>The browser and OS for people, e.g. <c>Chrome on Windows</c> (null = not recognised).</summary>
    public string? Device { get; init; }

    /// <summary>Signed in with "remember me".</summary>
    public bool Persistent { get; init; }

    /// <summary>Authentication methods used.</summary>
    public IReadOnlyList<string> AuthenticationMethods { get; init; } = [];

    /// <summary>True for the session making this request.</summary>
    public bool IsCurrent { get; init; }
}
