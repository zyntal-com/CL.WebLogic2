using CL.WebLogic.Auth.Events;
using CL.WebLogic.Auth.Notifications;
using CL.WebLogic.Auth.Passwords;
using CL.WebLogic.Auth.Tokens;
using CL.WebLogic.Auth.Users;
using CodeLogic.Core.Results;

namespace CL.WebLogic.Auth.Accounts;

public sealed partial class WebAccountService
{
    /// <summary>Queues a password reset link. Behaves the same whether or not the address exists.</summary>
    public async Task RequestPasswordResetAsync(string email, CancellationToken cancellationToken)
    {
        var user = await FindByEmailAsync(email, cancellationToken).ConfigureAwait(false);
        if (user is null || user.IsDisabled)
            return;
        var tokens = _config.Current.Auth.Tokens;
        var token = _tokens.Create(WebTokenPurposes.PasswordReset, user, TimeSpan.FromMinutes(tokens.PasswordResetMinutes));
        _notifications.Enqueue(user, WebSecurityNotificationKind.PasswordReset, BuildLink(tokens.PasswordResetPath, token));
    }

    /// <summary>Sets a new password from a reset link: every session of the user ends and the lockout is cleared.</summary>
    public async Task<Result<WebUser>> ResetPasswordAsync(string token, string newPassword, CancellationToken cancellationToken)
    {
        var validated = await _tokens.ValidateAsync(WebTokenPurposes.PasswordReset, token, consume: false, cancellationToken).ConfigureAwait(false);
        if (validated.IsFailure)
            return Result<WebUser>.Failure(validated.Error!);

        var policy = _policy.Validate(newPassword, validated.Value!.User);
        if (policy.IsFailure)
            return Result<WebUser>.Failure(policy.Error!);

        // Spend the link only once the new password is acceptable.
        var spent = await _tokens.ValidateAsync(WebTokenPurposes.PasswordReset, token, consume: true, cancellationToken).ConfigureAwait(false);
        if (spent.IsFailure)
            return Result<WebUser>.Failure(spent.Error!);

        var user = spent.Value!.User with { EmailConfirmed = true, FailedAccessCount = 0, LockoutEnd = null };
        return await SetPasswordAsync(user, newPassword, viaReset: true, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Changes a password after checking the current one. The caller keeps the current session.
    /// This check is not throttled: request handlers should use <c>request.Auth.ChangePasswordAsync</c>,
    /// which checks the current password through the sign-in throttle and lockout.
    /// </summary>
    public async Task<Result<WebUser>> ChangePasswordAsync(WebUser user, string currentPassword, string newPassword, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        if (user.PasswordHash is null || _hasher.Verify(user.PasswordHash, currentPassword ?? string.Empty) == WebPasswordVerification.Failed)
            return Fail<WebUser>("weblogic.auth.wrong_password", "The current password is not correct.");
        return await SetPasswordAsync(user, newPassword, viaReset: false, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Sets a password without checking the old one (resets, required changes, administrators).
    /// Applies the policy, changes the security stamp (other sessions and links stop working) and
    /// tells the user.
    /// </summary>
    public async Task<Result<WebUser>> SetPasswordAsync(WebUser user, string newPassword, bool viaReset, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        var policy = _policy.Validate(newPassword, user);
        if (policy.IsFailure)
            return Result<WebUser>.Failure(policy.Error!);

        var updated = user with
        {
            PasswordHash = _hasher.Hash(newPassword),
            SecurityStamp = WebUser.NewStamp(),
            RequiresPasswordChange = false
        };
        await _users.UpdateAsync(updated, cancellationToken).ConfigureAwait(false);
        if (viaReset)
            await _sessions.DeleteForUserAsync(user.Id, cancellationToken).ConfigureAwait(false);

        _context.Events.Publish(new PasswordChangedEvent(user.Id, viaReset));
        _notifications.Enqueue(updated, WebSecurityNotificationKind.PasswordChanged);
        return Result<WebUser>.Success(updated);
    }
}
