using System.Net;

namespace CL.WebLogic.Auth.Notifications;

/// <summary>Built-in English texts for security e-mails (used when no mail template exists).</summary>
public static class SecurityNotificationText
{
    /// <summary>Subject, plain text and HTML for <paramref name="notification"/>.</summary>
    public static (string Subject, string Text, string Html) Render(WebSecurityNotification notification, string siteName)
    {
        ArgumentNullException.ThrowIfNull(notification);
        var (subject, body) = notification.Kind switch
        {
            WebSecurityNotificationKind.PasswordReset => ("Reset your password", "Use the link below to choose a new password. If you did not ask for this, ignore this e-mail."),
            WebSecurityNotificationKind.EmailVerification => ("Confirm your e-mail address", "Use the link below to confirm your e-mail address."),
            WebSecurityNotificationKind.MagicLink => ("Your sign-in link", "Use the link below to sign in. It works once and expires soon."),
            WebSecurityNotificationKind.EmailChangeConfirmation => ("Confirm your new e-mail address", "Use the link below to confirm this as your new e-mail address."),
            WebSecurityNotificationKind.EmailChanged => ("Your e-mail address was changed", "The e-mail address of your account was changed. If this was not you, contact us right away."),
            WebSecurityNotificationKind.PasswordChanged => ("Your password was changed", "The password of your account was changed. If this was not you, reset your password right away."),
            WebSecurityNotificationKind.TwoFactorEnabled => ("Two-factor authentication is on", "Two-factor authentication was turned on for your account."),
            WebSecurityNotificationKind.TwoFactorDisabled => ("Two-factor authentication is off", "Two-factor authentication was turned off for your account. If this was not you, reset your password right away."),
            WebSecurityNotificationKind.RecoveryCodeUsed => ("A recovery code was used", "A recovery code was used to sign in to your account."),
            WebSecurityNotificationKind.AccountLockedOut => ("Your account was locked", "Your account was locked for a while after several failed sign-in attempts."),
            WebSecurityNotificationKind.ApiKeyCreated => ("A new API key was created", "A new API key was created for your account."),
            WebSecurityNotificationKind.NewDeviceSignIn => ("New sign-in to your account",
                "Your account was signed in to from a new browser or device" + (notification.Values.TryGetValue("device", out var device) ? ": " + device : "")
                + ". If this was not you, change your password and sign out everywhere."),
            WebSecurityNotificationKind.PasskeyAdded => ("A passkey was added", "A passkey was added to your account. If this was not you, remove it and reset your password right away."),
            WebSecurityNotificationKind.PasskeyRemoved => ("A passkey was removed", "A passkey was removed from your account."),
            _ => ("Account notice", "There was a change to your account.")
        };

        var greeting = "Hello " + notification.UserName + ",";
        var text = greeting + "\n\n" + body + (notification.Link is null ? "" : "\n\n" + notification.Link) + "\n\n" + siteName + "\n";
        var html = "<p>" + WebUtility.HtmlEncode(greeting) + "</p><p>" + WebUtility.HtmlEncode(body) + "</p>"
            + (notification.Link is null ? "" : "<p><a href=\"" + WebUtility.HtmlEncode(notification.Link) + "\">" + WebUtility.HtmlEncode(notification.Link) + "</a></p>")
            + "<p>" + WebUtility.HtmlEncode(siteName) + "</p>";
        return (siteName + ": " + subject, text, html);
    }
}
