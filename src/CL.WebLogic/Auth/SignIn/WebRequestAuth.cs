using CL.WebLogic.Auth.Users;
using CL.WebLogic.Runtime.Services;
using CL.WebLogic.Security.Identity;
using CodeLogic.Core.Results;

namespace CL.WebLogic.Auth.SignIn;

/// <summary>
/// <c>request.Auth</c>: the sign-in flows bound to the current request. Every method uses the
/// request's cancellation token unless one is passed.
/// </summary>
public sealed class WebRequestAuth
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

    /// <summary>Password sign-in; may continue with <see cref="CompleteTwoFactorAsync"/> or <see cref="CompletePasswordChangeAsync"/>.</summary>
    public Task<WebSignInResult> PasswordSignInAsync(string login, string password, CancellationToken cancellationToken = default) =>
        _service.PasswordSignInAsync(_request.Security, login, password, Token(cancellationToken));

    /// <summary>Second step with an authenticator code.</summary>
    public Task<WebSignInResult> CompleteTwoFactorAsync(string code, CancellationToken cancellationToken = default) =>
        _service.CompleteTwoFactorAsync(_request.Security, code, Token(cancellationToken));

    /// <summary>Second step with a recovery code.</summary>
    public Task<WebSignInResult> RedeemRecoveryCodeAsync(string code, CancellationToken cancellationToken = default) =>
        _service.RedeemRecoveryCodeAsync(_request.Security, code, Token(cancellationToken));

    /// <summary>True while the session waits for the second factor.</summary>
    public Task<bool> IsSecondFactorPendingAsync(CancellationToken cancellationToken = default) =>
        _service.IsSecondFactorPendingAsync(_request.Security, Token(cancellationToken));

    /// <summary>Sign-in from an e-mailed link.</summary>
    public Task<WebSignInResult> SignInWithMagicLinkAsync(string token, CancellationToken cancellationToken = default) =>
        _service.SignInWithMagicLinkAsync(_request.Security, token, Token(cancellationToken));

    /// <summary>Sets a required new password and finishes signing in.</summary>
    public Task<WebSignInResult> CompletePasswordChangeAsync(string token, string newPassword, CancellationToken cancellationToken = default) =>
        _service.CompletePasswordChangeAsync(_request.Security, token, newPassword, Token(cancellationToken));

    /// <summary>Signs a user in directly (after registration or external login).</summary>
    public Task SignInAsync(WebUser user, IReadOnlyList<string>? methods = null, CancellationToken cancellationToken = default) =>
        _service.SignInAsync(_request.Security, user, methods, Token(cancellationToken));

    /// <summary>Signs out.</summary>
    public Task SignOutAsync(bool everywhere = false, CancellationToken cancellationToken = default) =>
        _service.SignOutAsync(_request.Security, everywhere, Token(cancellationToken));

    /// <summary>Reloads the user's groups and permissions.</summary>
    public Task<bool> RefreshAsync(CancellationToken cancellationToken = default) =>
        _service.RefreshAsync(_request.Security, Token(cancellationToken));

    /// <summary>Changes the password (ends the user's other sessions).</summary>
    public Task<Result> ChangePasswordAsync(string currentPassword, string newPassword, CancellationToken cancellationToken = default) =>
        _service.ChangePasswordAsync(_request.Security, currentPassword, newPassword, Token(cancellationToken));

    /// <summary>The signed-in user from the store.</summary>
    public Task<WebUser?> GetCurrentUserAsync(CancellationToken cancellationToken = default) =>
        _service.GetCurrentUserAsync(_request.Security, Token(cancellationToken));

    /// <summary>A local return URL, or "/" for anything unsafe (absolute, protocol-relative, control characters…).</summary>
    public static string GetSafeReturnUrl(string? returnUrl) => WebUrl.SafeReturnUrl(returnUrl);

    private CancellationToken Token(CancellationToken cancellationToken) =>
        cancellationToken.CanBeCanceled ? cancellationToken : _request.RequestAborted;
}
