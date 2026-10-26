using CL.WebLogic.Auth.Users;
using CodeLogic.Core.Results;

namespace CL.WebLogic.Auth.SignIn;

/// <summary>Result of a sign-in attempt. Show users only a generic message for failures.</summary>
public sealed class WebSignInResult
{
    /// <summary>The outcome.</summary>
    public required WebSignInStatus Status { get; init; }

    /// <summary>The user, when the credentials were valid.</summary>
    public WebUser? User { get; init; }

    /// <summary>When throttled or locked out: how long to wait.</summary>
    public TimeSpan? RetryAfter { get; init; }

    /// <summary>For <see cref="WebSignInStatus.RequiresPasswordChange"/>: a short-lived token for the set-new-password step.</summary>
    public string? PasswordChangeToken { get; init; }

    /// <summary>The suspension, when <see cref="Status"/> is <see cref="WebSignInStatus.Suspended"/>.</summary>
    public WebSuspension? Suspension { get; init; }

    /// <summary>
    /// Why a step was refused, when the reason may be shown to the user: the password policy
    /// message when <see cref="WebAuthService.CompletePasswordChangeAsync"/> rejects the new password.
    /// </summary>
    public Error? Error { get; init; }

    /// <summary>True when this attempt caused the lockout.</summary>
    public bool LockedNow { get; init; }

    /// <summary>True when signed in.</summary>
    public bool Succeeded => Status == WebSignInStatus.Succeeded;

    /// <summary>A result with just a status.</summary>
    public static WebSignInResult Of(WebSignInStatus status, WebUser? user = null, TimeSpan? retryAfter = null) =>
        new() { Status = status, User = user, RetryAfter = retryAfter };

    /// <summary>A <see cref="WebSignInStatus.Suspended"/> result.</summary>
    public static WebSignInResult Suspended(WebUser user, WebSuspension suspension) =>
        new() { Status = WebSignInStatus.Suspended, User = user, Suspension = suspension };
}
