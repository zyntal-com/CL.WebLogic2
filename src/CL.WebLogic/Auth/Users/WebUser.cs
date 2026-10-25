namespace CL.WebLogic.Auth.Users;

/// <summary>
/// A user as the auth flows see it. The app's <see cref="IWebUserStore"/> maps it to its own
/// storage. Immutable: flows produce updated copies (<c>with</c>) and save them.
/// </summary>
public sealed record WebUser
{
    /// <summary>Stable id.</summary>
    public required string Id { get; init; }

    /// <summary>Login name.</summary>
    public required string UserName { get; init; }

    /// <summary>E-mail address.</summary>
    public string? Email { get; init; }

    /// <summary>True once the e-mail address was confirmed.</summary>
    public bool EmailConfirmed { get; init; }

    /// <summary>Password hash (see <c>IWebPasswordHasher</c>), or null for password-less accounts.</summary>
    public string? PasswordHash { get; init; }

    /// <summary>
    /// Changes whenever credentials change (password, 2FA, e-mail). Sessions and account tokens
    /// bound to an older stamp stop working.
    /// </summary>
    public string SecurityStamp { get; init; } = NewStamp();

    /// <summary>Groups.</summary>
    public IReadOnlyList<string> Groups { get; init; } = [];

    /// <summary>Permissions granted directly.</summary>
    public IReadOnlyList<string> Permissions { get; init; } = [];

    /// <summary>Tenant.</summary>
    public string? TenantId { get; init; }

    /// <summary>Disabled accounts cannot sign in.</summary>
    public bool IsDisabled { get; init; }

    /// <summary>The user must choose a new password at the next sign-in.</summary>
    public bool RequiresPasswordChange { get; init; }

    /// <summary>Locked until this time (UTC), when set.</summary>
    public DateTimeOffset? LockoutEnd { get; init; }

    /// <summary>Consecutive failed sign-ins.</summary>
    public int FailedAccessCount { get; init; }

    /// <summary>The DataProtection-protected TOTP secret; null when two-factor is off.</summary>
    public string? TwoFactorSecret { get; init; }

    /// <summary>True when two-factor authentication is on.</summary>
    public bool TwoFactorEnabled => TwoFactorSecret is not null;

    /// <summary>Creation time.</summary>
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>A new random security stamp.</summary>
    public static string NewStamp() => Convert.ToHexStringLower(System.Security.Cryptography.RandomNumberGenerator.GetBytes(16));
}
