namespace CL.WebLogic.Auth.Accounts;

/// <summary>
/// The <c>Error.Code</c> values the auth and account services return. Compare against these
/// constants (never parse the code text) to put a failure on the right form field. The error's
/// kind (<c>ValidationError</c>, <c>ConflictError</c>…) gives the HTTP status; see
/// <see cref="Results.WebErrorStatus"/>.
/// </summary>
public static class WebAuthErrorCodes
{
    /// <summary>The user name is empty or too long.</summary>
    public const string InvalidUserName = "weblogic.auth.invalid_user_name";

    /// <summary>The e-mail address is not valid.</summary>
    public const string InvalidEmail = "weblogic.auth.invalid_email";

    /// <summary>The user name or e-mail address is already in use (a conflict; stores return it too).</summary>
    public const string DuplicateUser = "weblogic.auth.duplicate_user";

    /// <summary>The new e-mail address is already in use.</summary>
    public const string EmailTaken = "weblogic.auth.email_taken";

    /// <summary>The current password is not correct.</summary>
    public const string WrongPassword = "weblogic.auth.wrong_password";

    /// <summary>The new password does not meet the password policy.</summary>
    public const string PasswordPolicy = "weblogic.auth.password_policy";

    /// <summary>Too many attempts; try again shortly.</summary>
    public const string Throttled = "weblogic.auth.throttled";

    /// <summary>The account is locked out.</summary>
    public const string LockedOut = "weblogic.auth.locked_out";

    /// <summary>The operation needs a signed-in user.</summary>
    public const string NotSignedIn = "weblogic.auth.not_signed_in";

    /// <summary>No sign-in is waiting for a second factor or passkey.</summary>
    public const string NoPendingSignIn = "weblogic.auth.no_pending_sign_in";

    /// <summary>The account does not exist.</summary>
    public const string UserNotFound = "weblogic.auth.user_not_found";

    /// <summary>The user store cannot delete accounts.</summary>
    public const string DeleteUnsupported = "weblogic.auth.delete_unsupported";

    /// <summary>No user store is configured (the null default).</summary>
    public const string NoUserStore = "weblogic.auth.no_user_store";

    /// <summary>The account token (reset, confirmation, link) is invalid, expired or spent.</summary>
    public const string TokenInvalid = "weblogic.auth.token_invalid";

    /// <summary>The second-factor code is not valid.</summary>
    public const string CodeInvalid = "weblogic.auth.code_invalid";

    /// <summary>The two-factor enrollment is missing or expired.</summary>
    public const string EnrollmentInvalid = "weblogic.auth.enrollment_invalid";

    /// <summary>The external sign-in attempt expired or its state did not match.</summary>
    public const string ExternalState = "weblogic.auth.external_state";

    /// <summary>The external login is not linked to the user.</summary>
    public const string ExternalLoginNotFound = "weblogic.auth.external_login_not_found";
}
