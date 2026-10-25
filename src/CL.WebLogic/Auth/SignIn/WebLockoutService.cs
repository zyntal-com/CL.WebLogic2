using CL.WebLogic.Auth.Users;
using CL.WebLogic.Configuration.Services;

namespace CL.WebLogic.Auth.SignIn;

/// <summary>Counts failed sign-ins (password or second factor) and locks accounts.</summary>
public sealed class WebLockoutService
{
    private readonly IWebUserStore _users;
    private readonly WebLogicConfigHolder _config;
    private readonly TimeProvider _time;

    /// <summary>Creates the service.</summary>
    public WebLockoutService(IWebUserStore users, WebLogicConfigHolder config, TimeProvider? time = null)
    {
        _users = users ?? throw new ArgumentNullException(nameof(users));
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _time = time ?? TimeProvider.System;
    }

    /// <summary>True (with the remaining time) while <paramref name="user"/> is locked.</summary>
    public bool IsLockedOut(WebUser user, out TimeSpan remaining)
    {
        ArgumentNullException.ThrowIfNull(user);
        var now = _time.GetUtcNow();
        remaining = user.LockoutEnd is { } end && end > now ? end - now : TimeSpan.Zero;
        return remaining > TimeSpan.Zero;
    }

    /// <summary>Records a failure; returns the updated user (locked once the limit is reached).</summary>
    public async Task<WebUser> RecordFailureAsync(WebUser user, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        var lockout = _config.Current.Auth.Lockout;
        var lockUntil = _time.GetUtcNow().AddMinutes(lockout.LockoutMinutes);
        if (_users is IWebUserLockoutStore lockouts)
            return await lockouts.RecordFailedAccessAsync(user.Id, lockout.MaxFailedAttempts, lockUntil, cancellationToken).ConfigureAwait(false) ?? user;

        var count = user.FailedAccessCount + 1;
        var locks = lockout.MaxFailedAttempts > 0 && count >= lockout.MaxFailedAttempts;
        var updated = user with { FailedAccessCount = locks ? 0 : count, LockoutEnd = locks ? lockUntil : user.LockoutEnd };
        await _users.UpdateAsync(updated, cancellationToken).ConfigureAwait(false);
        return updated;
    }

    /// <summary>Clears failures and lockout; returns the updated user.</summary>
    public async Task<WebUser> ResetAsync(WebUser user, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        if (user.FailedAccessCount == 0 && user.LockoutEnd is null)
            return user;
        var updated = user with { FailedAccessCount = 0, LockoutEnd = null };
        if (_users is IWebUserLockoutStore lockouts)
            await lockouts.ResetFailedAccessAsync(user.Id, cancellationToken).ConfigureAwait(false);
        else
            await _users.UpdateAsync(updated, cancellationToken).ConfigureAwait(false);
        return updated;
    }
}
