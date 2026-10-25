using System.Globalization;
using CL.WebLogic.Auth.Accounts;
using CL.WebLogic.Auth.Events;
using CL.WebLogic.Auth.Notifications;
using CL.WebLogic.Auth.Throttling;
using CL.WebLogic.Auth.Tokens;
using CL.WebLogic.Auth.TwoFactor;
using CL.WebLogic.Auth.Users;
using CL.WebLogic.Configuration.Services;
using CL.WebLogic.Hosting.Services;
using CL.WebLogic.Security.Audit;
using CL.WebLogic.Security.Pipeline;
using CL.WebLogic.Security.Sessions;

namespace CL.WebLogic.Auth.SignIn;

/// <summary>
/// The sign-in flows: password, second factor (TOTP or recovery code), magic link, required
/// password change, sign-out and refresh. Request handlers use it through <c>request.Auth</c>.
/// </summary>
public sealed partial class WebAuthService
{
    internal const string PendingUntilKey = "__wl_2fa_until";

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
    private readonly TimeProvider _time;

    /// <summary>Creates the service.</summary>
    public WebAuthService(WebSessionManager sessions, IWebSessionStore sessionStore, IWebCredentialValidator validator, IWebUserStore users,
        WebTwoFactorService twoFactor, IWebSecurityTokenService tokens, IWebLoginThrottle throttle, WebLockoutService lockout,
        WebAccountService accounts, WebNotificationDispatcher notifications, IWebAuditLog audit, WebLogicConfigHolder config,
        IWebLogicContext context, TimeProvider? time = null, WebAttemptGate? gate = null)
    {
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

    /// <summary>Checks the password and signs in, or moves on to the second factor / password change.</summary>
    public async Task<WebSignInResult> PasswordSignInAsync(WebSecurityContext security, string login, string password, CancellationToken cancellationToken)
    {
        var result = await _validator.ValidateAsync(login, password, security, cancellationToken).ConfigureAwait(false);
        return await ContinueAsync(security, result, ["pwd"], cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Signs <paramref name="user"/> in directly (after registration, external login…). Rotates the session.</summary>
    public async Task SignInAsync(WebSecurityContext security, WebUser user, IReadOnlyList<string>? methods, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(security);
        ArgumentNullException.ThrowIfNull(user);
        methods ??= ["custom"];
        await _sessions.SignInAsync(security, new WebSignIn
        {
            UserId = user.Id,
            UserName = user.UserName,
            Groups = user.Groups,
            Permissions = user.Permissions,
            AuthenticationMethods = methods,
            TenantId = user.TenantId ?? security.TenantId,
            SecurityStamp = user.SecurityStamp
        }, cancellationToken).ConfigureAwait(false);
        (await _sessions.GetAsync(security, cancellationToken).ConfigureAwait(false)).Remove(PendingUntilKey);

        _context.Events.Publish(new UserSignedInEvent(user.Id, security.Identity.Scheme, methods));
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
        await SignInAsync(security, user, session.AuthenticationMethods, cancellationToken).ConfigureAwait(false);
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
        var user = await GetCurrentUserAsync(security, cancellationToken).ConfigureAwait(false);
        if (user is null)
            return CodeLogic.Core.Results.Result.Failure(CodeLogic.Core.Results.Error.Unauthorized("weblogic.auth.not_signed_in", "Sign in first."));

        var changed = await _accounts.ChangePasswordAsync(user, currentPassword, newPassword, cancellationToken).ConfigureAwait(false);
        if (changed.IsFailure)
            return CodeLogic.Core.Results.Result.Failure(changed.Error!);

        var session = await _sessions.GetAsync(security, cancellationToken).ConfigureAwait(false);
        await _sessionStore.DeleteForUserAsync(user.Id, cancellationToken).ConfigureAwait(false);
        await SignInAsync(security, changed.Value!, session.AuthenticationMethods, cancellationToken).ConfigureAwait(false);
        return CodeLogic.Core.Results.Result.Success();
    }

    private async Task<WebSignInResult> ContinueAsync(WebSecurityContext security, WebSignInResult result, IReadOnlyList<string> methods, CancellationToken cancellationToken)
    {
        switch (result.Status)
        {
            case WebSignInStatus.Succeeded:
                await SignInAsync(security, result.User!, methods, cancellationToken).ConfigureAwait(false);
                return result;
            case WebSignInStatus.RequiresTwoFactor:
                await BeginSecondFactorAsync(security, result.User!, methods, cancellationToken).ConfigureAwait(false);
                return result;
            case WebSignInStatus.RequiresPasswordChange:
                return PasswordChangeRequired(result.User!, methods);
            default:
                Failed(security, result);
                return result.Status == WebSignInStatus.NotAllowed ? result : WebSignInResult.Of(result.Status, retryAfter: result.RetryAfter);
        }
    }

    private async Task BeginSecondFactorAsync(WebSecurityContext security, WebUser user, IReadOnlyList<string> methods, CancellationToken cancellationToken)
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
        (await _sessions.GetAsync(security, cancellationToken).ConfigureAwait(false)).SetString(PendingUntilKey, until.ToString(CultureInfo.InvariantCulture));
    }

    private WebSignInResult PasswordChangeRequired(WebUser user, IReadOnlyList<string> methods) => new()
    {
        Status = WebSignInStatus.RequiresPasswordChange,
        User = user,
        PasswordChangeToken = _tokens.Create(WebTokenPurposes.PasswordChange, user, TimeSpan.FromMinutes(15), string.Join(',', methods))
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
