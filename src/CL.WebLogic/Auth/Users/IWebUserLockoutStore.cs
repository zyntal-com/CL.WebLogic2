namespace CL.WebLogic.Auth.Users;

/// <summary>Atomic failed-sign-in counting (avoids lost updates under parallel attempts).</summary>
public interface IWebUserLockoutStore
{
    /// <summary>Increments the failure count and locks the account until <paramref name="lockUntil"/> once it reaches <paramref name="maxFailures"/>; returns the updated user.</summary>
    Task<WebUser?> RecordFailedAccessAsync(string userId, int maxFailures, DateTimeOffset lockUntil, CancellationToken cancellationToken);

    /// <summary>Clears the failure count and lockout.</summary>
    Task ResetFailedAccessAsync(string userId, CancellationToken cancellationToken);
}
