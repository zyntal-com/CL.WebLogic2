using CL.WebLogic.Auth.Passwords;
using CL.WebLogic.Auth.Throttling;
using CL.WebLogic.Auth.Users;
using CL.WebLogic.Configuration.Services;
using CL.WebLogic.Security.Pipeline;

namespace CL.WebLogic.Auth.SignIn;

/// <summary>
/// Default credential check. Unknown users still cost one hash computation, so response time
/// does not reveal which logins exist; every failure is reported as
/// <see cref="WebSignInStatus.InvalidCredentials"/>. Users of another tenant are treated as unknown.
/// </summary>
public sealed class WebCredentialValidator : IWebCredentialValidator
{
    private readonly IWebUserStore _users;
    private readonly IWebPasswordHasher _hasher;
    private readonly IWebLoginThrottle _throttle;
    private readonly WebLockoutService _lockout;
    private readonly WebLogicConfigHolder _config;
    private readonly Lazy<string> _dummyHash;
    private readonly WebAttemptGate _gate;
    private readonly TimeProvider _time;

    /// <summary>Creates the validator.</summary>
    public WebCredentialValidator(IWebUserStore users, IWebPasswordHasher hasher, IWebLoginThrottle throttle, WebLockoutService lockout, WebLogicConfigHolder config,
        WebAttemptGate? gate = null, TimeProvider? time = null)
    {
        _gate = gate ?? new WebAttemptGate();
        _time = time ?? TimeProvider.System;
        _users = users ?? throw new ArgumentNullException(nameof(users));
        _hasher = hasher ?? throw new ArgumentNullException(nameof(hasher));
        _throttle = throttle ?? throw new ArgumentNullException(nameof(throttle));
        _lockout = lockout ?? throw new ArgumentNullException(nameof(lockout));
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _dummyHash = new Lazy<string>(() => _hasher.Hash(Guid.NewGuid().ToString("N")));
    }

    /// <inheritdoc/>
    public async Task<WebSignInResult> ValidateAsync(string login, string password, WebSecurityContext security, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(security);
        var auth = _config.Current.Auth;
        login = (login ?? string.Empty).Trim();
        password ??= string.Empty;

        using var attempt = _gate.TryEnter("pwd:" + login);
        if (attempt is null)
            return WebSignInResult.Of(WebSignInStatus.Throttled, retryAfter: TimeSpan.FromSeconds(1));
        if (await _throttle.CheckAsync(login, security.ClientIp, cancellationToken).ConfigureAwait(false) is { } wait)
            return WebSignInResult.Of(WebSignInStatus.Throttled, retryAfter: wait);

        WebUser? user = null;
        if (login.Length > 0 && login.Length <= 256 && password.Length <= auth.Passwords.MaximumLength)
        {
            user = await _users.FindByLoginAsync(login, cancellationToken).ConfigureAwait(false);
            if (user is not null && security.TenantId is not null && user.TenantId is not null
                && !string.Equals(user.TenantId, security.TenantId, StringComparison.Ordinal))
                user = null;
        }

        var verification = _hasher.Verify(user?.PasswordHash ?? _dummyHash.Value, user is null ? "\0" : password);
        if (user?.PasswordHash is null)
            verification = WebPasswordVerification.Failed;

        // A locked account answers LockedOut whatever the password: answering differently for the
        // right one would let guessing go on during the lock with a free "correct" signal. (Locking
        // already shows the account exists.) A wrong password does not extend the lock.
        var remaining = TimeSpan.Zero;
        var locked = user is not null && _lockout.IsLockedOut(user, out remaining);

        if (verification == WebPasswordVerification.Failed)
        {
            await _throttle.RecordFailureAsync(login, security.ClientIp, cancellationToken).ConfigureAwait(false);
            if (user is null)
                return WebSignInResult.Of(WebSignInStatus.InvalidCredentials);
            if (locked)
                return WebSignInResult.Of(WebSignInStatus.LockedOut, retryAfter: remaining);
            var updated = await _lockout.RecordFailureAsync(user, cancellationToken).ConfigureAwait(false);
            return _lockout.IsLockedOut(updated, out var lockedFor)
                ? new WebSignInResult { Status = WebSignInStatus.LockedOut, User = updated, RetryAfter = lockedFor, LockedNow = true }
                : WebSignInResult.Of(WebSignInStatus.InvalidCredentials);
        }

        if (locked)
            return WebSignInResult.Of(WebSignInStatus.LockedOut, retryAfter: remaining);

        await _throttle.RecordSuccessAsync(login, security.ClientIp, cancellationToken).ConfigureAwait(false);
        var verified = await _lockout.ResetAsync(user!, cancellationToken).ConfigureAwait(false);
        if (verification == WebPasswordVerification.SuccessRehashNeeded)
        {
            verified = verified with { PasswordHash = _hasher.Hash(password) };
            await _users.UpdateAsync(verified, cancellationToken).ConfigureAwait(false);
        }

        if (verified.IsDisabled || (auth.RequireConfirmedEmail && !verified.EmailConfirmed))
            return WebSignInResult.Of(WebSignInStatus.NotAllowed, verified);
        if (WebSuspension.Of(verified, _time.GetUtcNow()) is { } suspension)
            return WebSignInResult.Suspended(verified, suspension);
        if (verified.TwoFactorEnabled)
            return WebSignInResult.Of(WebSignInStatus.RequiresTwoFactor, verified);
        if (verified.RequiresPasswordChange)
            return WebSignInResult.Of(WebSignInStatus.RequiresPasswordChange, verified);
        return WebSignInResult.Of(WebSignInStatus.Succeeded, verified);
    }
}
