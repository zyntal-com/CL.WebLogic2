namespace CL.WebLogic.Auth.SignIn;

/// <summary>Outcome of a credential check or sign-in step.</summary>
public enum WebSignInStatus
{
    /// <summary>Signed in.</summary>
    Succeeded,

    /// <summary>Unknown login or wrong password/code (deliberately indistinguishable).</summary>
    InvalidCredentials,

    /// <summary>The account is locked for a while.</summary>
    LockedOut,

    /// <summary>The account may not sign in (disabled, e-mail not confirmed).</summary>
    NotAllowed,

    /// <summary>Password accepted; a second factor is required next.</summary>
    RequiresTwoFactor,

    /// <summary>Password accepted, but it must be changed first (see <see cref="WebSignInResult.PasswordChangeToken"/>).</summary>
    RequiresPasswordChange,

    /// <summary>Too many attempts; retry later (see <see cref="WebSignInResult.RetryAfter"/>).</summary>
    Throttled,

    /// <summary>
    /// The account is suspended (see <see cref="WebSignInResult.Suspension"/>). Only reported after
    /// the credentials were proven, so it reveals nothing to someone guessing passwords.
    /// </summary>
    Suspended
}
