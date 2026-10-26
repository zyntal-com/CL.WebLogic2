namespace CL.WebLogic.Auth.Notifications;

/// <summary>Kinds of security e-mail.</summary>
public enum WebSecurityNotificationKind
{
    /// <summary>Link to set a new password.</summary>
    PasswordReset,

    /// <summary>Link to confirm the e-mail address.</summary>
    EmailVerification,

    /// <summary>Sign-in link.</summary>
    MagicLink,

    /// <summary>Link to confirm a new address (sent to the new address).</summary>
    EmailChangeConfirmation,

    /// <summary>The address was changed (sent to the old address).</summary>
    EmailChanged,

    /// <summary>The password was changed.</summary>
    PasswordChanged,

    /// <summary>Two-factor authentication was turned on.</summary>
    TwoFactorEnabled,

    /// <summary>Two-factor authentication was turned off.</summary>
    TwoFactorDisabled,

    /// <summary>A recovery code was used.</summary>
    RecoveryCodeUsed,

    /// <summary>The account was locked after failed sign-ins.</summary>
    AccountLockedOut,

    /// <summary>An API key was created.</summary>
    ApiKeyCreated,

    /// <summary>A sign-in from a browser none of the user's other sessions use (<c>Auth.Notifications.NotifyNewDevice</c>).</summary>
    NewDeviceSignIn,

    /// <summary>A passkey was added (value <c>name</c>).</summary>
    PasskeyAdded,

    /// <summary>A passkey was removed (value <c>name</c>).</summary>
    PasskeyRemoved
}
