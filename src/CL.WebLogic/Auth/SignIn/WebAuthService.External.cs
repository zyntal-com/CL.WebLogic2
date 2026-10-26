using CL.WebLogic.Auth.Users;
using CL.WebLogic.Security.Identity;
using CL.WebLogic.Security.Pipeline;
using CL.WebLogic.Security.Sessions;

namespace CL.WebLogic.Auth.SignIn;

public sealed partial class WebAuthService
{
    /// <summary>
    /// Signs in a user an external provider vouched for (after <c>WebExternalLoginFlow</c>). Unlike
    /// <see cref="SignInAsync"/> it applies the account rules: disabled, other-tenant and locked
    /// accounts are refused, and users with two-factor authentication continue with
    /// <see cref="CompleteTwoFactorAsync"/>. Recorded as method <c>ext:{provider}</c>.
    /// </summary>
    public Task<WebSignInResult> SignInExternalAsync(WebSecurityContext security, WebUser user, string provider, bool persistent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(security);
        ArgumentNullException.ThrowIfNull(user);
        ArgumentException.ThrowIfNullOrWhiteSpace(provider);
        return ContinueAsync(security, Evaluate(security, user), ["ext:" + provider.Trim().ToLowerInvariant()], persistent, cancellationToken);
    }

    /// <summary>
    /// Proves the signed-in user again for step-up checks (<c>RequireRecentSignIn</c>): the password,
    /// plus the authenticator code when the user has two-factor authentication. Without a code such
    /// users get <see cref="WebSignInStatus.RequiresTwoFactor"/> and nothing changes. On success the
    /// authentication time is now, the methods are exactly the ones just verified and the session id
    /// rotates. Accounts without a password re-authenticate by signing in again (link or provider).
    /// </summary>
    public async Task<WebSignInResult> ReauthenticateAsync(WebSecurityContext security, string password, string? twoFactorCode, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(security);
        var session = await _sessions.GetAsync(security, cancellationToken).ConfigureAwait(false);
        if (session.AuthStage != WebSessionAuthStage.Authenticated || session.UserId is null || security.Identity.Scheme != WebAuthenticationSchemes.Session)
            return WebSignInResult.Of(WebSignInStatus.NotAllowed);
        var user = await _users.FindByIdAsync(session.UserId, cancellationToken).ConfigureAwait(false);
        if (user is null || user.IsDisabled || user.PasswordHash is null)
            return WebSignInResult.Of(WebSignInStatus.NotAllowed);

        var checkedPassword = await _validator.ValidateAsync(user.UserName, password, security, cancellationToken).ConfigureAwait(false);
        if (checkedPassword.Status is not (WebSignInStatus.Succeeded or WebSignInStatus.RequiresTwoFactor or WebSignInStatus.RequiresPasswordChange)
            || checkedPassword.User?.Id != user.Id)
        {
            Failed(security, checkedPassword);
            return WebSignInResult.Of(checkedPassword.Status == WebSignInStatus.NotAllowed ? WebSignInStatus.NotAllowed : checkedPassword.Status, retryAfter: checkedPassword.RetryAfter);
        }

        user = checkedPassword.User;
        IReadOnlyList<string> methods = ["pwd"];
        if (user.TwoFactorEnabled)
        {
            if (string.IsNullOrWhiteSpace(twoFactorCode))
                return WebSignInResult.Of(WebSignInStatus.RequiresTwoFactor, user);
            var (failure, verified) = await VerifyFactorAsync(security, user, u => _twoFactor.VerifyCodeAsync(u, twoFactorCode, cancellationToken), cancellationToken).ConfigureAwait(false);
            if (failure is not null)
                return failure;
            user = verified!;
            methods = ["pwd", "otp"];
        }

        await SignInCoreAsync(security, user, methods, fresh: true, persistent: null, cancellationToken).ConfigureAwait(false);
        _audit.Record(security, "auth.reauthenticate", "success", data: new Dictionary<string, string> { ["methods"] = string.Join(',', methods) });
        return WebSignInResult.Of(WebSignInStatus.Succeeded, user);
    }

    /// <summary>The signed-in user's sessions ("signed-in devices"); empty when the store cannot list them.</summary>
    public async Task<IReadOnlyList<WebSessionInfo>> ListSessionsAsync(WebSecurityContext security, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(security);
        if (security.Identity.UserId is not { } userId || security.Identity.Scheme != WebAuthenticationSchemes.Session)
            return [];
        return await _devices.ListAsync(userId, await CurrentIdHashAsync(security, cancellationToken).ConfigureAwait(false), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Ends one of the signed-in user's sessions (signs out when it is the current one). False when it is not theirs.</summary>
    public async Task<bool> RevokeSessionAsync(WebSecurityContext security, string idHash, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(security);
        if (security.Identity.UserId is not { } userId || security.Identity.Scheme != WebAuthenticationSchemes.Session || string.IsNullOrEmpty(idHash))
            return false;
        if (string.Equals(idHash, await CurrentIdHashAsync(security, cancellationToken).ConfigureAwait(false), StringComparison.Ordinal))
        {
            await SignOutAsync(security, false, cancellationToken).ConfigureAwait(false);
            return true;
        }
        if (!await _devices.RevokeAsync(userId, idHash, cancellationToken).ConfigureAwait(false))
            return false;
        _audit.Record(security, "auth.session", "revoked", data: new Dictionary<string, string> { ["user"] = userId });
        return true;
    }

    private async Task<string?> CurrentIdHashAsync(WebSecurityContext security, CancellationToken cancellationToken)
    {
        var session = await _sessions.GetAsync(security, cancellationToken).ConfigureAwait(false);
        return session.Token is { } token ? WebSessionTokens.Hash(token) : session.Loaded?.IdHash;
    }
}
