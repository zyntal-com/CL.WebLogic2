using System.Text.Json;
using CL.WebLogic.Auth.Passkeys;
using CL.WebLogic.Auth.Users;
using CL.WebLogic.Security.Identity;
using CL.WebLogic.Security.Pipeline;
using CL.WebLogic.Security.Sessions;
using CodeLogic.Core.Results;

namespace CL.WebLogic.Auth.SignIn;

public sealed partial class WebAuthService
{
    private WebPasskeyService Passkeys => _passkeys ?? throw new InvalidOperationException("Passkeys are not registered (WebPasskeyService).");

    /// <summary>Starts passwordless sign-in: the browser offers the user's passkeys for this site.</summary>
    public Task<Result<WebPasskeyChallenge>> BeginPasskeySignInAsync(WebSecurityContext security, CancellationToken cancellationToken) =>
        Passkeys.BeginAssertionAsync(security, WebPasskeyPurpose.SignIn, null, cancellationToken);

    /// <summary>
    /// Signs in with a passkey (user verification required, so it counts as multi-factor: no TOTP
    /// step). Disabled, other-tenant and suspended accounts are refused and a required password
    /// change still applies. The failed-attempt lockout does not block passkeys (it guards
    /// passwords) and is cleared. Failures are throttled per credential and client address but never
    /// count towards lockout: credential ids are not secret. Recorded as method <c>passkey</c>.
    /// </summary>
    public async Task<WebSignInResult> PasskeySignInAsync(WebSecurityContext security, WebPasskeyRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(security);
        ArgumentNullException.ThrowIfNull(request);
        var throttleKey = "passkey:" + CredentialId(request);
        using var attempt = _gate.TryEnter(throttleKey);
        if (attempt is null)
            return WebSignInResult.Of(WebSignInStatus.Throttled, retryAfter: TimeSpan.FromSeconds(1));
        if (await _throttle.CheckAsync(throttleKey, security.ClientIp, cancellationToken).ConfigureAwait(false) is { } wait)
            return WebSignInResult.Of(WebSignInStatus.Throttled, retryAfter: wait);

        var verified = await Passkeys.VerifyAssertionAsync(security, WebPasskeyPurpose.SignIn, request, cancellationToken).ConfigureAwait(false);
        if (verified.IsFailure)
        {
            await _throttle.RecordFailureAsync(throttleKey, security.ClientIp, cancellationToken).ConfigureAwait(false);
            Failed(security, WebSignInResult.Of(WebSignInStatus.InvalidCredentials));
            return WebSignInResult.Of(WebSignInStatus.InvalidCredentials);
        }
        await _throttle.RecordSuccessAsync(throttleKey, security.ClientIp, cancellationToken).ConfigureAwait(false);

        var user = verified.Value!.User;
        var result = EvaluatePasskey(security, user);
        if (result.Status == WebSignInStatus.Succeeded && (user.FailedAccessCount > 0 || user.LockoutEnd is not null))
            result = WebSignInResult.Of(WebSignInStatus.Succeeded, await _lockout.ResetAsync(user, cancellationToken).ConfigureAwait(false));
        return await ContinueAsync(security, result, ["passkey"], request.Remember, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Starts the passkey second factor for the session waiting for one.</summary>
    public async Task<Result<WebPasskeyChallenge>> BeginPasskeySecondFactorAsync(WebSecurityContext security, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(security);
        var session = await _sessions.GetAsync(security, cancellationToken).ConfigureAwait(false);
        var user = session.AuthStage == WebSessionAuthStage.PendingSecondFactor && !PendingExpired(session) && session.UserId is { } id
            ? await _users.FindByIdAsync(id, cancellationToken).ConfigureAwait(false)
            : null;
        return user is null
            ? Result<WebPasskeyChallenge>.Failure(Error.Unauthorized("weblogic.auth.no_pending_sign_in", "Sign in with your password first."))
            : await Passkeys.BeginAssertionAsync(security, WebPasskeyPurpose.SecondFactor, user, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Completes a pending sign-in with one of the user's passkeys instead of a TOTP code.</summary>
    public Task<WebSignInResult> CompleteTwoFactorWithPasskeyAsync(WebSecurityContext security, WebPasskeyRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return SecondFactorAsync(security, "passkey", async user =>
        {
            var verified = await Passkeys.VerifyAssertionAsync(security, WebPasskeyPurpose.SecondFactor, request, cancellationToken).ConfigureAwait(false);
            return verified.IsSuccess && verified.Value!.User.Id == user.Id;
        }, cancellationToken);
    }

    /// <summary>Starts step-up with a passkey for the signed-in user.</summary>
    public async Task<Result<WebPasskeyChallenge>> BeginPasskeyReauthenticationAsync(WebSecurityContext security, CancellationToken cancellationToken) =>
        await Passkeys.BeginAssertionAsync(security, WebPasskeyPurpose.Reauthentication, await SessionUserAsync(security, cancellationToken).ConfigureAwait(false), cancellationToken).ConfigureAwait(false);

    /// <summary>
    /// Proves the signed-in user again with one of their passkeys (user verification required), for
    /// <c>RequireRecentSignIn</c>. Like <see cref="ReauthenticateAsync"/>: the authentication time is
    /// now, the methods are exactly <c>passkey</c> and the session id rotates.
    /// </summary>
    public async Task<WebSignInResult> ReauthenticateWithPasskeyAsync(WebSecurityContext security, WebPasskeyRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var user = await SessionUserAsync(security, cancellationToken).ConfigureAwait(false);
        if (user is null || user.IsDisabled)
            return WebSignInResult.Of(WebSignInStatus.NotAllowed);

        var (failure, verified) = await VerifyFactorAsync(security, user, async u =>
        {
            var assertion = await Passkeys.VerifyAssertionAsync(security, WebPasskeyPurpose.Reauthentication, request, cancellationToken).ConfigureAwait(false);
            return assertion.IsSuccess && assertion.Value!.User.Id == u.Id;
        }, cancellationToken).ConfigureAwait(false);
        if (failure is not null)
            return failure;

        await SignInCoreAsync(security, verified!, ["passkey"], fresh: true, persistent: null, cancellationToken).ConfigureAwait(false);
        _audit.Record(security, "auth.reauthenticate", "success", data: new Dictionary<string, string> { ["methods"] = "passkey" });
        return WebSignInResult.Of(WebSignInStatus.Succeeded, verified);
    }

    /// <summary>Starts registering a passkey for the signed-in user.</summary>
    public async Task<Result<WebPasskeyChallenge>> BeginPasskeyRegistrationAsync(WebSecurityContext security, CancellationToken cancellationToken) =>
        await SessionUserAsync(security, cancellationToken).ConfigureAwait(false) is { } user
            ? await Passkeys.BeginRegistrationAsync(security, user, cancellationToken).ConfigureAwait(false)
            : Result<WebPasskeyChallenge>.Failure(Error.Unauthorized("weblogic.auth.not_signed_in", "Sign in first."));

    /// <summary>Finishes registering a passkey for the signed-in user.</summary>
    public async Task<Result<WebPasskey>> CompletePasskeyRegistrationAsync(WebSecurityContext security, WebPasskeyRequest request, CancellationToken cancellationToken) =>
        await SessionUserAsync(security, cancellationToken).ConfigureAwait(false) is { } user
            ? await Passkeys.CompleteRegistrationAsync(security, user, request, cancellationToken).ConfigureAwait(false)
            : Result<WebPasskey>.Failure(Error.Unauthorized("weblogic.auth.not_signed_in", "Sign in first."));

    /// <summary>The signed-in user's passkeys (empty when not signed in with a session).</summary>
    public async Task<IReadOnlyList<WebPasskey>> ListPasskeysAsync(WebSecurityContext security, CancellationToken cancellationToken) =>
        await SessionUserAsync(security, cancellationToken).ConfigureAwait(false) is { } user
            ? await Passkeys.ListAsync(user, cancellationToken).ConfigureAwait(false)
            : [];

    /// <summary>Renames one of the signed-in user's passkeys.</summary>
    public async Task<bool> RenamePasskeyAsync(WebSecurityContext security, string credentialId, string name, CancellationToken cancellationToken) =>
        await SessionUserAsync(security, cancellationToken).ConfigureAwait(false) is { } user
        && await Passkeys.RenameAsync(user, credentialId, name, cancellationToken).ConfigureAwait(false);

    /// <summary>Removes one of the signed-in user's passkeys.</summary>
    public async Task<bool> DeletePasskeyAsync(WebSecurityContext security, string credentialId, CancellationToken cancellationToken) =>
        await SessionUserAsync(security, cancellationToken).ConfigureAwait(false) is { } user
        && await Passkeys.DeleteAsync(security, user, credentialId, cancellationToken).ConfigureAwait(false);

    /// <summary>Account rules after a verified passkey: like <see cref="Evaluate"/>, minus lockout and the TOTP step.</summary>
    private WebSignInResult EvaluatePasskey(WebSecurityContext security, WebUser user) =>
        user.IsDisabled || (user.TenantId is not null && security.TenantId is not null && !string.Equals(user.TenantId, security.TenantId, StringComparison.Ordinal))
            ? WebSignInResult.Of(WebSignInStatus.NotAllowed, user)
        : WebSuspension.Of(user, _time.GetUtcNow()) is { } suspension ? WebSignInResult.Suspended(user, suspension)
        : user.RequiresPasswordChange && user.PasswordHash is not null ? WebSignInResult.Of(WebSignInStatus.RequiresPasswordChange, user)
        : WebSignInResult.Of(WebSignInStatus.Succeeded, user);

    /// <summary>The user signed in with a session cookie (passkeys are never managed with API keys or tokens).</summary>
    private async Task<WebUser?> SessionUserAsync(WebSecurityContext security, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(security);
        return security.Identity.Scheme == WebAuthenticationSchemes.Session && security.Identity.UserId is { } id
            ? await _users.FindByIdAsync(id, cancellationToken).ConfigureAwait(false)
            : null;
    }

    private static string CredentialId(WebPasskeyRequest request)
    {
        var id = request.Credential.ValueKind == JsonValueKind.Object && request.Credential.TryGetProperty("id", out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()!
            : "";
        return id.Length is > 0 and <= 1400 ? id : "?";
    }
}
