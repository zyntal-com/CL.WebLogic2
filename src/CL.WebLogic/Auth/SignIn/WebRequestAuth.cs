using CL.WebLogic.Auth.Users;
using CL.WebLogic.Runtime.Services;
using CL.WebLogic.Security.Identity;
using CL.WebLogic.Security.Sessions;
using CodeLogic.Core.Results;

namespace CL.WebLogic.Auth.SignIn;

/// <summary>
/// <c>request.Auth</c>: the sign-in flows bound to the current request. Every method uses the
/// request's cancellation token unless one is passed.
/// </summary>
public sealed partial class WebRequestAuth
{
    private readonly Runtime.Models.WebRequestContext _request;
    private readonly WebAuthService _service;

    internal WebRequestAuth(Runtime.Models.WebRequestContext request, WebAuthService service)
    {
        _request = request;
        _service = service;
    }

    /// <summary>The current identity.</summary>
    public WebIdentity Identity => _request.Identity;

    /// <summary>True when signed in.</summary>
    public bool IsAuthenticated => _request.Identity.IsAuthenticated;

    /// <summary>
    /// Password sign-in; may continue with <see cref="CompleteTwoFactorAsync"/> or
    /// <see cref="CompletePasswordChangeAsync"/>. <paramref name="persistent"/> is "remember me".
    /// </summary>
    public Task<WebSignInResult> PasswordSignInAsync(string login, string password, bool persistent = false, CancellationToken cancellationToken = default) =>
        _service.PasswordSignInAsync(_request.Security, login, password, persistent, Token(cancellationToken));

    /// <summary>Second step with an authenticator code.</summary>
    public Task<WebSignInResult> CompleteTwoFactorAsync(string code, CancellationToken cancellationToken = default) =>
        _service.CompleteTwoFactorAsync(_request.Security, code, Token(cancellationToken));

    /// <summary>Second step with a recovery code.</summary>
    public Task<WebSignInResult> RedeemRecoveryCodeAsync(string code, CancellationToken cancellationToken = default) =>
        _service.RedeemRecoveryCodeAsync(_request.Security, code, Token(cancellationToken));

    /// <summary>True while the session waits for the second factor.</summary>
    public Task<bool> IsSecondFactorPendingAsync(CancellationToken cancellationToken = default) =>
        _service.IsSecondFactorPendingAsync(_request.Security, Token(cancellationToken));

    /// <summary>Sign-in from an e-mailed link (users with two-factor authentication continue with their code).</summary>
    public Task<WebSignInResult> SignInWithMagicLinkAsync(string token, bool persistent = false, CancellationToken cancellationToken = default) =>
        _service.SignInWithMagicLinkAsync(_request.Security, token, persistent, Token(cancellationToken));

    /// <summary>
    /// Sign-in for a user an external provider vouched for. Applies the account rules and asks
    /// users with two-factor authentication for their code (<see cref="CompleteTwoFactorAsync"/>).
    /// </summary>
    public Task<WebSignInResult> SignInExternalAsync(WebUser user, string provider, bool persistent = false, CancellationToken cancellationToken = default) =>
        _service.SignInExternalAsync(_request.Security, user, provider, persistent, Token(cancellationToken));

    /// <summary>
    /// Proves the signed-in user again (password, plus the authenticator code when they use
    /// two-factor authentication) for routes with <c>RequireRecentSignIn</c>.
    /// </summary>
    public Task<WebSignInResult> ReauthenticateAsync(string password, string? twoFactorCode = null, CancellationToken cancellationToken = default) =>
        _service.ReauthenticateAsync(_request.Security, password, twoFactorCode, Token(cancellationToken));

    /// <summary>Sets a required new password and finishes signing in.</summary>
    public Task<WebSignInResult> CompletePasswordChangeAsync(string token, string newPassword, CancellationToken cancellationToken = default) =>
        _service.CompletePasswordChangeAsync(_request.Security, token, newPassword, Token(cancellationToken));

    /// <summary>
    /// Signs a user in directly, after your own verification (e.g. right after registration).
    /// Skips the second factor: for external providers use <see cref="SignInExternalAsync"/>.
    /// <paramref name="persistent"/>: "remember me"; null keeps the current session's setting when the same user is
    /// signed in again (e.g. after changing their methods) and means "not persistent" for anyone else.
    /// </summary>
    public Task SignInAsync(WebUser user, IReadOnlyList<string>? methods = null, bool? persistent = null, CancellationToken cancellationToken = default) =>
        _service.SignInAsync(_request.Security, user, methods, persistent, Token(cancellationToken));

    /// <summary>Signs out.</summary>
    public Task SignOutAsync(bool everywhere = false, CancellationToken cancellationToken = default) =>
        _service.SignOutAsync(_request.Security, everywhere, Token(cancellationToken));

    /// <summary>Reloads the user's groups and permissions.</summary>
    public Task<bool> RefreshAsync(CancellationToken cancellationToken = default) =>
        _service.RefreshAsync(_request.Security, Token(cancellationToken));

    /// <summary>Changes the password (ends the user's other sessions).</summary>
    public Task<Result> ChangePasswordAsync(string currentPassword, string newPassword, CancellationToken cancellationToken = default) =>
        _service.ChangePasswordAsync(_request.Security, currentPassword, newPassword, Token(cancellationToken));

    /// <summary>The signed-in user's sessions ("signed-in devices"). Needs a store implementing <see cref="IWebSessionListStore"/>.</summary>
    public Task<IReadOnlyList<WebSessionInfo>> ListSessionsAsync(CancellationToken cancellationToken = default) =>
        _service.ListSessionsAsync(_request.Security, Token(cancellationToken));

    /// <summary>Ends one of the signed-in user's sessions ("sign out that device").</summary>
    public Task<bool> RevokeSessionAsync(string idHash, CancellationToken cancellationToken = default) =>
        _service.RevokeSessionAsync(_request.Security, idHash, Token(cancellationToken));

    /// <summary>The signed-in user from the store.</summary>
    public Task<WebUser?> GetCurrentUserAsync(CancellationToken cancellationToken = default) =>
        _service.GetCurrentUserAsync(_request.Security, Token(cancellationToken));

    /// <summary>A local return URL, or "/" for anything unsafe (absolute, protocol-relative, control characters…).</summary>
    public static string GetSafeReturnUrl(string? returnUrl) => WebUrl.SafeReturnUrl(returnUrl);

    private CancellationToken Token(CancellationToken cancellationToken) =>
        cancellationToken.CanBeCanceled ? cancellationToken : _request.RequestAborted;
}
