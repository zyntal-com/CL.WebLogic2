using System.Globalization;
using CL.WebLogic.Auth.Tokens;
using CL.WebLogic.Auth.Users;
using CL.WebLogic.Security.Pipeline;
using CL.WebLogic.Security.Sessions;

namespace CL.WebLogic.Auth.SignIn;

public sealed partial class WebAuthService
{
    /// <summary>Completes a pending sign-in with a TOTP code.</summary>
    public Task<WebSignInResult> CompleteTwoFactorAsync(WebSecurityContext security, string code, CancellationToken cancellationToken) =>
        SecondFactorAsync(security, "otp", user => _twoFactor.VerifyCodeAsync(user, code, cancellationToken), cancellationToken);

    /// <summary>Completes a pending sign-in with a recovery code (the code is spent).</summary>
    public Task<WebSignInResult> RedeemRecoveryCodeAsync(WebSecurityContext security, string code, CancellationToken cancellationToken) =>
        SecondFactorAsync(security, "recovery", user => _twoFactor.RedeemRecoveryCodeAsync(user, code, cancellationToken), cancellationToken);

    /// <summary>True while the session waits for the second factor.</summary>
    public async Task<bool> IsSecondFactorPendingAsync(WebSecurityContext security, CancellationToken cancellationToken)
    {
        var session = await _sessions.GetAsync(security, cancellationToken).ConfigureAwait(false);
        return session.AuthStage == WebSessionAuthStage.PendingSecondFactor && !PendingExpired(session);
    }

    /// <summary>Signs in from an e-mailed link (the link is spent; it also confirms the address).</summary>
    public async Task<WebSignInResult> SignInWithMagicLinkAsync(WebSecurityContext security, string token, CancellationToken cancellationToken)
    {
        var validated = await _tokens.ValidateAsync(WebTokenPurposes.MagicLink, token, consume: true, cancellationToken).ConfigureAwait(false);
        if (validated.IsFailure)
        {
            Failed(security, WebSignInResult.Of(WebSignInStatus.InvalidCredentials));
            return WebSignInResult.Of(WebSignInStatus.InvalidCredentials);
        }

        var user = validated.Value!.User;
        if (!user.EmailConfirmed)
        {
            // The address was never proven, so whoever registered it may not own it: a password
            // set then (account pre-hijacking) is dropped and their sessions end. The owner can set
            // a password later through a reset link.
            user = user with { EmailConfirmed = true, PasswordHash = null, SecurityStamp = WebUser.NewStamp() };
            await _users.UpdateAsync(user, cancellationToken).ConfigureAwait(false);
            await _sessionStore.DeleteForUserAsync(user.Id, cancellationToken).ConfigureAwait(false);
        }
        var result = user.IsDisabled ? WebSignInResult.Of(WebSignInStatus.NotAllowed, user)
            : _lockout.IsLockedOut(user, out var remaining) ? WebSignInResult.Of(WebSignInStatus.LockedOut, retryAfter: remaining)
            : user.TwoFactorEnabled ? WebSignInResult.Of(WebSignInStatus.RequiresTwoFactor, user)
            : user.RequiresPasswordChange && user.PasswordHash is not null ? WebSignInResult.Of(WebSignInStatus.RequiresPasswordChange, user)
            : WebSignInResult.Of(WebSignInStatus.Succeeded, user);
        return await ContinueAsync(security, result, ["link"], cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Sets the required new password (token from <see cref="WebSignInResult.PasswordChangeToken"/>) and finishes signing in.</summary>
    public async Task<WebSignInResult> CompletePasswordChangeAsync(WebSecurityContext security, string token, string newPassword, CancellationToken cancellationToken)
    {
        var validated = await _tokens.ValidateAsync(WebTokenPurposes.PasswordChange, token, consume: false, cancellationToken).ConfigureAwait(false);
        if (validated.IsFailure)
            return WebSignInResult.Of(WebSignInStatus.InvalidCredentials);

        var changed = await _accounts.SetPasswordAsync(validated.Value!.User, newPassword, viaReset: false, cancellationToken).ConfigureAwait(false);
        if (changed.IsFailure)
            return new WebSignInResult { Status = WebSignInStatus.RequiresPasswordChange, User = validated.Value.User, PasswordChangeToken = token };

        var methods = (validated.Value.Data ?? "pwd").Split(',', StringSplitOptions.RemoveEmptyEntries);
        var user = changed.Value!;
        var secondFactorDone = methods.Contains("otp") || methods.Contains("recovery");
        var result = user.TwoFactorEnabled && !secondFactorDone
            ? WebSignInResult.Of(WebSignInStatus.RequiresTwoFactor, user)
            : WebSignInResult.Of(WebSignInStatus.Succeeded, user);
        return await ContinueAsync(security, result, methods, cancellationToken).ConfigureAwait(false);
    }

    private async Task<WebSignInResult> SecondFactorAsync(WebSecurityContext security, string method, Func<WebUser, Task<bool>> verify, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(security);
        var session = await _sessions.GetAsync(security, cancellationToken).ConfigureAwait(false);
        if (session.AuthStage != WebSessionAuthStage.PendingSecondFactor || session.UserId is null)
            return WebSignInResult.Of(WebSignInStatus.InvalidCredentials);
        if (PendingExpired(session))
        {
            await _sessions.SignOutAsync(security, false, cancellationToken).ConfigureAwait(false);
            return WebSignInResult.Of(WebSignInStatus.InvalidCredentials);
        }

        var throttleKey = "2fa:" + session.UserId;
        using var attempt = _gate.TryEnter(throttleKey);
        if (attempt is null)
            return WebSignInResult.Of(WebSignInStatus.Throttled, retryAfter: TimeSpan.FromSeconds(1));
        if (await _throttle.CheckAsync(throttleKey, security.ClientIp, cancellationToken).ConfigureAwait(false) is { } wait)
            return WebSignInResult.Of(WebSignInStatus.Throttled, retryAfter: wait);

        var user = await _users.FindByIdAsync(session.UserId, cancellationToken).ConfigureAwait(false);
        if (user is null || user.IsDisabled || !string.Equals(user.SecurityStamp, session.SecurityStamp, StringComparison.Ordinal))
        {
            await _sessions.SignOutAsync(security, false, cancellationToken).ConfigureAwait(false);
            return WebSignInResult.Of(WebSignInStatus.InvalidCredentials);
        }
        if (_lockout.IsLockedOut(user, out var remaining))
            return WebSignInResult.Of(WebSignInStatus.LockedOut, retryAfter: remaining);

        if (!await verify(user).ConfigureAwait(false))
        {
            await _throttle.RecordFailureAsync(throttleKey, security.ClientIp, cancellationToken).ConfigureAwait(false);
            var updated = await _lockout.RecordFailureAsync(user, cancellationToken).ConfigureAwait(false);
            var locked = _lockout.IsLockedOut(updated, out var lockedFor);
            var failure = locked
                ? new WebSignInResult { Status = WebSignInStatus.LockedOut, User = updated, RetryAfter = lockedFor, LockedNow = true }
                : new WebSignInResult { Status = WebSignInStatus.InvalidCredentials, User = updated };
            Failed(security, failure);
            return WebSignInResult.Of(failure.Status, retryAfter: failure.RetryAfter);
        }

        await _throttle.RecordSuccessAsync(throttleKey, security.ClientIp, cancellationToken).ConfigureAwait(false);
        user = await _lockout.ResetAsync(user, cancellationToken).ConfigureAwait(false);
        var methods = (IReadOnlyList<string>)[.. session.AuthenticationMethods, method];
        if (user.RequiresPasswordChange)
            return PasswordChangeRequired(user, methods);

        await SignInAsync(security, user, methods, cancellationToken).ConfigureAwait(false);
        return WebSignInResult.Of(WebSignInStatus.Succeeded, user);
    }

    private bool PendingExpired(LazyWebSession session) =>
        !long.TryParse(session.GetString(PendingUntilKey), NumberStyles.None, CultureInfo.InvariantCulture, out var until)
        || until < _time.GetUtcNow().ToUnixTimeSeconds();
}
