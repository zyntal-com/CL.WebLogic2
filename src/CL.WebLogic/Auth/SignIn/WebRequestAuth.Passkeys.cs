using CL.WebLogic.Auth.Passkeys;
using CodeLogic.Core.Results;

namespace CL.WebLogic.Auth.SignIn;

public sealed partial class WebRequestAuth
{
    /// <summary>Starts passwordless sign-in; return the challenge as JSON to the page.</summary>
    public Task<Result<WebPasskeyChallenge>> BeginPasskeySignInAsync(CancellationToken cancellationToken = default) =>
        _service.BeginPasskeySignInAsync(_request.Security, Token(cancellationToken));

    /// <summary>
    /// Signs in with a passkey (no TOTP step: the passkey is multi-factor). May continue with
    /// <see cref="CompletePasswordChangeAsync"/>; <see cref="WebPasskeyRequest.Remember"/> is "remember me".
    /// </summary>
    public Task<WebSignInResult> PasskeySignInAsync(WebPasskeyRequest request, CancellationToken cancellationToken = default) =>
        _service.PasskeySignInAsync(_request.Security, request, Token(cancellationToken));

    /// <summary>Starts the passkey second factor for a pending password sign-in.</summary>
    public Task<Result<WebPasskeyChallenge>> BeginPasskeySecondFactorAsync(CancellationToken cancellationToken = default) =>
        _service.BeginPasskeySecondFactorAsync(_request.Security, Token(cancellationToken));

    /// <summary>Completes a pending sign-in with a passkey instead of a TOTP code.</summary>
    public Task<WebSignInResult> CompleteTwoFactorWithPasskeyAsync(WebPasskeyRequest request, CancellationToken cancellationToken = default) =>
        _service.CompleteTwoFactorWithPasskeyAsync(_request.Security, request, Token(cancellationToken));

    /// <summary>Starts passkey step-up for the signed-in user.</summary>
    public Task<Result<WebPasskeyChallenge>> BeginPasskeyReauthenticationAsync(CancellationToken cancellationToken = default) =>
        _service.BeginPasskeyReauthenticationAsync(_request.Security, Token(cancellationToken));

    /// <summary>Proves the signed-in user again with a passkey, for routes with <c>RequireRecentSignIn</c>.</summary>
    public Task<WebSignInResult> ReauthenticateWithPasskeyAsync(WebPasskeyRequest request, CancellationToken cancellationToken = default) =>
        _service.ReauthenticateWithPasskeyAsync(_request.Security, request, Token(cancellationToken));

    /// <summary>Starts registering a passkey for the signed-in user (consider <c>RequireRecentSignIn</c> on the route).</summary>
    public Task<Result<WebPasskeyChallenge>> BeginPasskeyRegistrationAsync(CancellationToken cancellationToken = default) =>
        _service.BeginPasskeyRegistrationAsync(_request.Security, Token(cancellationToken));

    /// <summary>Finishes registering a passkey; <see cref="WebPasskeyRequest.Name"/> names it.</summary>
    public Task<Result<WebPasskey>> CompletePasskeyRegistrationAsync(WebPasskeyRequest request, CancellationToken cancellationToken = default) =>
        _service.CompletePasskeyRegistrationAsync(_request.Security, request, Token(cancellationToken));

    /// <summary>The signed-in user's passkeys.</summary>
    public Task<IReadOnlyList<WebPasskey>> ListPasskeysAsync(CancellationToken cancellationToken = default) =>
        _service.ListPasskeysAsync(_request.Security, Token(cancellationToken));

    /// <summary>Renames one of the signed-in user's passkeys.</summary>
    public Task<bool> RenamePasskeyAsync(string credentialId, string name, CancellationToken cancellationToken = default) =>
        _service.RenamePasskeyAsync(_request.Security, credentialId, name, Token(cancellationToken));

    /// <summary>Removes one of the signed-in user's passkeys (the user is notified).</summary>
    public Task<bool> DeletePasskeyAsync(string credentialId, CancellationToken cancellationToken = default) =>
        _service.DeletePasskeyAsync(_request.Security, credentialId, Token(cancellationToken));
}
