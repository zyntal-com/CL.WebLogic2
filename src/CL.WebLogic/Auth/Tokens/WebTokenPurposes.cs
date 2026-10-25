namespace CL.WebLogic.Auth.Tokens;

/// <summary>Purposes of account tokens. A token only validates for the purpose it was made for.</summary>
public static class WebTokenPurposes
{
    /// <summary>Set a new password from an e-mailed link.</summary>
    public const string PasswordReset = "password-reset";

    /// <summary>Confirm an e-mail address.</summary>
    public const string EmailVerification = "email-verification";

    /// <summary>Sign in from an e-mailed link.</summary>
    public const string MagicLink = "magic-link";

    /// <summary>Confirm a new e-mail address (the token carries it).</summary>
    public const string EmailChange = "email-change";

    /// <summary>Set a required new password right after sign-in.</summary>
    public const string PasswordChange = "password-change";

    /// <summary>Finish two-factor enrollment (the token carries the new secret).</summary>
    public const string TwoFactorEnrollment = "2fa-enrollment";
}
