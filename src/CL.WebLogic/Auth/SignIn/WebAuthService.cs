using System.Globalization;
using CL.WebLogic.Auth.Accounts;
using CL.WebLogic.Auth.Events;
using CL.WebLogic.Auth.Notifications;
using CL.WebLogic.Auth.Passkeys;
using CL.WebLogic.Auth.Sessions;
using CL.WebLogic.Auth.Throttling;
using CL.WebLogic.Auth.Tokens;
using CL.WebLogic.Auth.TwoFactor;
using CL.WebLogic.Auth.Users;
using CL.WebLogic.Configuration.Services;
using CL.WebLogic.Hosting.Services;
using CL.WebLogic.Security.Audit;
using CL.WebLogic.Security.Identity;
using CL.WebLogic.Security.Pipeline;
using CL.WebLogic.Security.Sessions;

namespace CL.WebLogic.Auth.SignIn;

/// <summary>
/// The sign-in flows: password, passkey, second factor (TOTP, passkey or recovery code), magic link,
/// external providers, required password change, re-authentication, sign-out, refresh and the
/// user's sessions and passkeys. Request handlers use it through <c>request.Auth</c>.
/// </summary>
public sealed partial class WebAuthService
{
    internal const string PendingUntilKey = "__wl_2fa_until";
    internal const string PendingPersistentKey = "__wl_2fa_persist";

    private readonly WebSessionManager _sessions;
    private readonly IWebSessionStore _sessionStore;
    private readonly IWebCredentialValidator _validator;
    private readonly IWebUserStore _users;
    private readonly WebTwoFactorService _twoFactor;
    private readonly IWebSecurityTokenService _tokens;
    private readonly IWebLoginThrottle _throttle;
    private readonly WebLockoutService _lockout;
    private readonly WebAccountService _accounts;
    private readonly WebNotificationDispatcher _notifications;
    private readonly IWebAuditLog _audit;
    private readonly WebLogicConfigHolder _config;
    private readonly IWebLogicContext _context;
    private readonly WebAttemptGate _gate;
    private readonly WebSessionDevices _devices;
    private readonly WebPasskeyService? _passkeys;
    private readonly TimeProvider _time;

    /// <summary>Creates the service.</summary>
    public WebAuthService(WebSessionManager sessions, IWebSessionStore sessionStore, IWebCredentialValidator validator, IWebUserStore users,
        WebTwoFactorService twoFactor, IWebSecurityTokenService tokens, IWebLoginThrottle throttle, WebLockoutService lockout,
        WebAccountService accounts, WebNotificationDispatcher notifications, IWebAuditLog audit, WebLogicConfigHolder config,
        IWebLogicContext context, WebSessionDevices devices, TimeProvider? time = null, WebAttemptGate? gate = null, WebPasskeyService? passkeys = null)
    {
        _passkeys = passkeys;
        _devices = devices ?? throw new ArgumentNullException(nameof(devices));
        _gate = gate ?? new WebAttemptGate();
        _sessions = sessions ?? throw new ArgumentNullException(nameof(sessions));
        _sessionStore = sessionStore ?? throw new ArgumentNullException(nameof(sessionStore));
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _users = users ?? throw new ArgumentNullException(nameof(users));
        _twoFactor = twoFactor ?? throw new ArgumentNullException(nameof(twoFactor));
        _tokens = tokens ?? throw new ArgumentNullException(nameof(tokens));
        _throttle = throttle ?? throw new ArgumentNullException(nameof(throttle));
        _lockout = lockout ?? throw new ArgumentNullException(nameof(lockout));
        _accounts = accounts ?? throw new ArgumentNullException(nameof(accounts));
        _notifications = notifications ?? throw new ArgumentNullException(nameof(notifications));
        _audit = audit ?? throw new ArgumentNullException(nameof(audit));
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _time = time ?? TimeProvider.System;
    }

    /// <summary>
    /// Checks the password and signs in, or moves on to the second factor / password change.
    /// <paramref name="persistent"/> is "remember me" and carries over to those next steps.
    /// </summary>
    public async Task<WebSignInResult> PasswordSignInAsync(WebSecurityContext security, string login, string password, bool persistent, CancellationToken cancellationToken)
    {
        var result = await _validator.ValidateAsync(login, password, security, cancellationToken).ConfigureAwait(false);
        return await ContinueAsync(security, result, ["pwd"], persistent, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Signs <paramref name="user"/> in directly, after the app verified them itself (registration,
    /// a custom flow). Rotates the session. This does <b>not</b> ask for the user's second factor:
    /// for external providers use <see cref="SignInExternalAsync"/>, which does.
    /// </summary>
    public Task SignInAsync(WebSecurityContext security, WebUser user, IReadOnlyList<string>? methods, bool? persistent, CancellationToken cancellationToken) =>
        SignInCoreAsync(security, user, methods ?? ["custom"], fresh: true, persistent, cancellationToken);

    /// <summary>
    /// Records the sign-in. <paramref name="fresh"/>: credentials were just verified (sets the
    /// authentication time, applies the per-user session cap and the new-device notice); otherwise
    /// the same user is re-signed (refresh, password change) and keeps time and "remember me".
    /// </summary>
    private async Task SignInCoreAsync(WebSecurityContext security, WebUser user, IReadOnlyList<string> methods, bool fresh, bool? persistent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(security);
        ArgumentNullException.ThrowIfNull(user);
        var session = await _sessions.GetAsync(security, cancellationToken).ConfigureAwait(false);
        var sameUser = session.AuthStage == WebSessionAuthStage.Authenticated && session.UserId == user.Id;
        if (fresh && !sameUser)
            await _devices.OnSigningInAsync(user, session.Loaded?.IdHash, WebSessionManager.UserAgent(security.HttpContext), cancellationToken).ConfigureAwait(false);

        await _sessions.SignInAsync(security, new WebSignIn
        {
            UserId = user.Id,
            UserName = user.UserName,
            Groups = user.Groups,
            Permissions = user.Permissions,
            AuthenticationMethods = methods,
            TenantId = user.TenantId ?? security.TenantId,
            SecurityStamp = user.SecurityStamp,
            AuthenticatedAt = fresh ? _time.GetUtcNow() : null,
            Persistent = persistent
        }, cancellationToken).ConfigureAwait(false);
        session.Remove(PendingUntilKey);
        session.Remove(PendingPersistentKey);

        _context.Events.Publish(new UserSignedInEvent(user.Id, security.Identity.Scheme, methods) { IsRefresh = !fresh });
        _audit.Record(security, "auth.signin", "success", data: new Dictionary<string, string> { ["methods"] = string.Join(',', methods) });
    }

    /// <summary>Signs out (of every session of the user when <paramref name="everywhere"/>).</summary>
    public async Task SignOutAsync(WebSecurityContext security, bool everywhere, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(security);
        var userId = security.Identity.UserId ?? (await _sessions.GetAsync(security, cancellationToken).ConfigureAwait(false)).UserId;
        await _sessions.SignOutAsync(security, everywhere, cancellationToken).ConfigureAwait(false);
        if (userId is not null)
        {
            _context.Events.Publish(new UserSignedOutEvent(userId, everywhere));
            _audit.Record(security, "auth.signout", everywhere ? "everywhere" : "success", data: new Dictionary<string, string> { ["user"] = userId });
        }
    }

    /// <summary>Reloads the signed-in user (new groups, permissions) into a fresh session; signs out when the user is gone or disabled.</summary>
    public async Task<bool> RefreshAsync(WebSecurityContext security, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(security);
        var session = await _sessions.GetAsync(security, cancellationToken).ConfigureAwait(false);
        if (session.AuthStage != WebSessionAuthStage.Authenticated || session.UserId is null)
            return false;
        var user = await _users.FindByIdAsync(session.UserId, cancellationToken).ConfigureAwait(false);
        if (user is null || user.IsDisabled)
        {
            await SignOutAsync(security, false, cancellationToken).ConfigureAwait(false);
            return false;
        }
        // A new stamp means the security settings changed (2FA, e-mail, password): like a password
        // change, every other session of the user ends and only this one continues.
        if (!string.Equals(user.SecurityStamp, session.SecurityStamp, StringComparison.Ordinal))
            await _sessionStore.DeleteForUserAsync(user.Id, cancellationToken).ConfigureAwait(false);
        await SignInCoreAsync(security, user, session.AuthenticationMethods, fresh: false, persistent: null, cancellationToken).ConfigureAwait(false);
        return true;
    }

    /// <summary>The signed-in user from the store, or null.</summary>
    public async Task<WebUser?> GetCurrentUserAsync(WebSecurityContext security, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(security);
        return security.Identity.UserId is { } id ? await _users.FindByIdAsync(id, cancellationToken).ConfigureAwait(false) : null;
    }

    /// <summary>Changes the signed-in user's password, ends their other sessions and keeps this one.</summary>
    public async Task<CodeLogic.Core.Results.Result> ChangePasswordAsync(WebSecurityContext security, string currentPassword, string newPassword, CancellationToken cancellationToken)
    {
        // Only a signed-in browser session: an API key or bearer token plus the password must not
        // turn into a session that skipped the second factor.
        ArgumentNullException.ThrowIfNull(security);
        var session = await _sessions.GetAsync(security, cancellationToken).ConfigureAwait(false);
        if (security.Identity.Scheme != WebAuthenticationSchemes.Session || session.AuthStage != WebSessionAuthStage.Authenticated
            || !string.Equals(session.UserId, security.Identity.UserId, StringComparison.Ordinal))
            return CodeLogic.Core.Results.Result.Failure(CodeLogic.Core.Results.Error.Unauthorized("weblogic.auth.not_signed_in", "Sign in first."));
        var user = await GetCurrentUserAsync(security, cancellationToken).ConfigureAwait(false);
        if (user is null)
            return CodeLogic.Core.Results.Result.Failure(CodeLogic.Core.Results.Error.Unauthorized("weblogic.auth.not_signed_in", "Sign in first."));

        // The current password goes through the sign-in checks (throttle, lockout), so a stolen
        // session cannot guess it without limit.
        if (user.PasswordHash is null)
            return CodeLogic.Core.Results.Result.Failure(CodeLogic.Core.Results.Error.Validation("weblogic.auth.wrong_password", "The current password is not correct."));
        var checkedPassword = await _validator.ValidateAsync(user.UserName, currentPassword ?? string.Empty, security, cancellationToken).ConfigureAwait(false);
        if (checkedPassword.Status is not (WebSignInStatus.Succeeded or WebSignInStatus.RequiresTwoFactor or WebSignInStatus.RequiresPasswordChange)
            || checkedPassword.User?.Id != user.Id)
        {
            Failed(security, checkedPassword);
            return CodeLogic.Core.Results.Result.Failure(checkedPassword.Status switch
            {
                WebSignInStatus.Throttled => CodeLogic.Core.Results.Error.Validation("weblogic.auth.throttled", "Too many attempts. Try again shortly."),
                WebSignInStatus.LockedOut => CodeLogic.Core.Results.Error.Validation("weblogic.auth.locked_out", "The account is locked. Try again later."),
                _ => CodeLogic.Core.Results.Error.Validation("weblogic.auth.wrong_password", "The current password is not correct.")
            });
        }

        var changed = await _accounts.SetPasswordAsync(checkedPassword.User, newPassword, viaReset: false, cancellationToken).ConfigureAwait(false);
        if (changed.IsFailure)
            return CodeLogic.Core.Results.Result.Failure(changed.Error!);

        await _sessionStore.DeleteForUserAsync(user.Id, cancellationToken).ConfigureAwait(false);
        await SignInCoreAsync(security, changed.Value!, session.AuthenticationMethods, fresh: false, persistent: null, cancellationToken).ConfigureAwait(false);
        return CodeLogic.Core.Results.Result.Success();
    }

    private async Task<WebSignInResult> ContinueAsync(WebSecurityContext security, WebSignInResult result, IReadOnlyList<string> methods, bool persistent, CancellationToken cancellationToken)
    {
        switch (result.Status)
        {
            case WebSignInStatus.Succeeded:
                await SignInCoreAsync(security, result.User!, methods, fresh: true, persistent, cancellationToken).ConfigureAwait(false);
                return result;
            case WebSignInStatus.RequiresTwoFactor:
                await BeginSecondFactorAsync(security, result.User!, methods, persistent, cancellationToken).ConfigureAwait(false);
                return result;
            case WebSignInStatus.RequiresPasswordChange:
                return PasswordChangeRequired(result.User!, methods, persistent);
            default:
                Failed(security, result);
                // Keep the suspension (reason, end) for the suspended page; drop the user otherwise.
                return result.Status == WebSignInStatus.NotAllowed ? result
                    : new WebSignInResult { Status = result.Status, RetryAfter = result.RetryAfter, Suspension = result.Suspension };
        }
    }

    private async Task BeginSecondFactorAsync(WebSecurityContext security, WebUser user, IReadOnlyList<string> methods, bool persistent, CancellationToken cancellationToken)
    {
        await _sessions.SignInAsync(security, new WebSignIn
        {
            UserId = user.Id,
            UserName = user.UserName,
            Stage = WebSessionAuthStage.PendingSecondFactor,
            AuthenticationMethods = methods,
            TenantId = user.TenantId ?? security.TenantId,
            SecurityStamp = user.SecurityStamp
        }, cancellationToken).ConfigureAwait(false);
        var until = _time.GetUtcNow().AddMinutes(_config.Current.Auth.TwoFactor.PendingMinutes).ToUnixTimeSeconds();
        var session = await _sessions.GetAsync(security, cancellationToken).ConfigureAwait(false);
        session.SetString(PendingUntilKey, until.ToString(CultureInfo.InvariantCulture));
        if (persistent)
            session.SetString(PendingPersistentKey, "1");
        else
            session.Remove(PendingPersistentKey);
        _context.Events.Publish(new SecondFactorRequiredEvent(user.Id, methods));
    }

    /// <summary>The token carries the methods used so far and "remember me": <c>pwd,otp|p</c>.</summary>
    private WebSignInResult PasswordChangeRequired(WebUser user, IReadOnlyList<string> methods, bool persistent) => new()
    {
        Status = WebSignInStatus.RequiresPasswordChange,
        User = user,
        PasswordChangeToken = _tokens.Create(WebTokenPurposes.PasswordChange, user, TimeSpan.FromMinutes(15), string.Join(',', methods) + (persistent ? "|p" : ""))
    };

    private void Failed(WebSecurityContext security, WebSignInResult result)
    {
        var data = new Dictionary<string, string>();
        if (result.User is not null)
            data["user"] = result.User.Id;
        _audit.Record(security, "auth.signin", result.Status.ToString().ToLowerInvariant(), data: data);
        _context.Events.Publish(new SignInFailedEvent(result.User?.Id, result.Status));

        if (result.LockedNow && result.User is not null)
        {
            _context.Events.Publish(new UserLockedOutEvent(result.User.Id, _time.GetUtcNow().Add(result.RetryAfter ?? TimeSpan.Zero)));
            _notifications.Enqueue(result.User, WebSecurityNotificationKind.AccountLockedOut);
        }
    }
}
