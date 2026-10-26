namespace CL.WebLogic.Security.Sessions;

/// <summary>What a session sign-in records. Built by the auth flows (or the app) after verifying credentials.</summary>
public sealed class WebSignIn
{
    /// <summary>User id.</summary>
    public required string UserId { get; init; }

    /// <summary>User name.</summary>
    public string? UserName { get; init; }

    /// <summary>Groups.</summary>
    public IReadOnlyList<string> Groups { get; init; } = [];

    /// <summary>Permissions granted directly (more may come from <c>IWebPermissionResolver</c>).</summary>
    public IReadOnlyList<string> Permissions { get; init; } = [];

    /// <summary>Methods used, e.g. <c>pwd</c>, <c>otp</c>.</summary>
    public IReadOnlyList<string> AuthenticationMethods { get; init; } = [];

    /// <summary>Tenant.</summary>
    public string? TenantId { get; init; }

    /// <summary>User security stamp.</summary>
    public string? SecurityStamp { get; init; }

    /// <summary>
    /// When the credentials were verified. Null keeps the session's time when the same user is
    /// signed in again (refresh, password change), otherwise "now".
    /// </summary>
    public DateTimeOffset? AuthenticatedAt { get; init; }

    /// <summary>"Remember me" (see <c>Session.PersistentIdleTimeoutDays</c>). Null keeps the session's setting for the same user.</summary>
    public bool? Persistent { get; init; }

    /// <summary><see cref="WebSessionAuthStage.PendingSecondFactor"/> when a second factor is still needed.</summary>
    public WebSessionAuthStage Stage { get; init; } = WebSessionAuthStage.Authenticated;
}
