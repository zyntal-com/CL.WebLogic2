using CL.WebLogic.Results;
using CL.WebLogic.Runtime.Models;
using StarterWebsite.Forms;

namespace StarterWebsite.Pages;

public sealed partial class AccountPages
{
    private const string ForgotTemplate = "templates/account/forgot-password";
    private const string ResetTemplate = "templates/account/reset-password";

    private static ValueTask<WebResult> ShowForgotPasswordAsync(WebRequestContext request, CancellationToken cancellationToken) =>
        ValueTask.FromResult<WebResult>(WebResult.Page(ForgotTemplate, new { Sent = request.GetQuery("sent") == "1" }, PageModels.Meta("Forgot password")));

    private async ValueTask<WebResult> ForgotPasswordAsync(WebRequestContext request, CancellationToken cancellationToken)
    {
        var submission = await request.Forms.BindAsync<ForgotPasswordForm>(cancellationToken).ConfigureAwait(false);
        if (!submission.IsValid)
            return submission.Invalid(fallbackTemplate: ForgotTemplate, fallbackModel: new { Sent = false });
        // Same answer whether or not the address exists: no account enumeration.
        await _accounts.RequestPasswordResetAsync(submission.Model.Email!, cancellationToken).ConfigureAwait(false);
        return FormResult.Success("If an account uses that address, a reset link is on its way.", "/account/forgot-password?sent=1");
    }

    private static ValueTask<WebResult> ShowResetPasswordAsync(WebRequestContext request, CancellationToken cancellationToken)
    {
        var token = request.GetQuery("token");
        if (string.IsNullOrEmpty(token))
            return ValueTask.FromResult(Redirect("/account/forgot-password"));
        request.Forms.Prefill(new ResetPasswordForm { Token = token });
        return ValueTask.FromResult<WebResult>(WebResult.Page(ResetTemplate, null, PageModels.Meta("Choose a new password")));
    }

    private async ValueTask<WebResult> ResetPasswordAsync(WebRequestContext request, CancellationToken cancellationToken)
    {
        var submission = await request.Forms.BindAsync<ResetPasswordForm>(cancellationToken).ConfigureAwait(false);
        if (!submission.IsValid)
            return submission.Invalid(fallbackTemplate: ResetTemplate);
        var reset = await _accounts.ResetPasswordAsync(submission.Model.Token!, submission.Model.NewPassword!, cancellationToken).ConfigureAwait(false);
        if (reset.IsFailure)
        {
            var message = reset.Error!.Code.Contains("password", StringComparison.OrdinalIgnoreCase) ? reset.Error.Message : "This link is invalid, expired or already used. Request a new one.";
            return submission.AddError(string.Empty, message).Invalid(message, ResetTemplate);
        }
        return FormResult.Success("Your password was changed. Sign in with the new one.", "/account/login?reset=1");
    }

    private async ValueTask<WebResult> VerifyEmailAsync(WebRequestContext request, CancellationToken cancellationToken)
    {
        var token = request.GetQuery("token") ?? string.Empty;
        var confirmed = token.Length > 0 && (await _accounts.ConfirmEmailAsync(token, cancellationToken).ConfigureAwait(false)).IsSuccess;
        return WebResult.Page("templates/account/verify-email", new { Confirmed = confirmed }, PageModels.Meta("E-mail verification"));
    }

    private async ValueTask<WebResult> ResendVerificationAsync(WebRequestContext request, CancellationToken cancellationToken)
    {
        var user = await request.Auth.GetCurrentUserAsync(cancellationToken).ConfigureAwait(false);
        if (user is { EmailConfirmed: false, Email: not null })
            _accounts.SendEmailVerification(user);
        return Redirect("/account?sent=1");
    }
}
