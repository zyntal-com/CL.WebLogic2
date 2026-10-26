using CL.WebLogic.Auth.Accounts;
using CL.WebLogic.Auth.SignIn;
using CL.WebLogic.Auth.Users;
using CL.WebLogic.Forms.Models;
using CL.WebLogic.Results;
using CL.WebLogic.Runtime.Models;
using StarterWebsite.Forms;

namespace StarterWebsite.Pages;

public sealed partial class AccountPages
{
    private const string LoginTemplate = "templates/account/login";
    private const string TwoFactorTemplate = "templates/account/two-factor";
    private const string RecoveryTemplate = "templates/account/recovery-code";
    private const string RegisterTemplate = "templates/account/register";

    private object LoginModel(WebRequestContext request) => new
    {
        Registration = _config.AllowRegistration,
        Providers = _external.Providers.Select(p => new { p.Name, p.DisplayName }).ToList(),
        Reset = request.GetQuery("reset") == "1"
    };

    private ValueTask<WebResult> ShowLoginAsync(WebRequestContext request, CancellationToken cancellationToken)
    {
        if (request.IsAuthenticated)
            return ValueTask.FromResult(Redirect("/account"));
        request.Forms.Prefill(new LoginForm { ReturnUrl = WebRequestAuth.GetSafeReturnUrl(request.GetQuery("returnUrl")) });
        return ValueTask.FromResult<WebResult>(WebResult.Page(LoginTemplate, LoginModel(request), PageModels.Meta("Sign in")));
    }

    private async ValueTask<WebResult> LoginAsync(WebRequestContext request, CancellationToken cancellationToken)
    {
        var submission = await request.Forms.BindAsync<LoginForm>(cancellationToken).ConfigureAwait(false);
        if (!submission.IsValid)
            return submission.Invalid(fallbackTemplate: LoginTemplate, fallbackModel: LoginModel(request));

        var next = WebRequestAuth.GetSafeReturnUrl(submission.Model.ReturnUrl);
        var result = await request.Auth.PasswordSignInAsync(submission.Model.Login!, submission.Model.Password!, submission.Model.RememberMe, cancellationToken).ConfigureAwait(false);
        if (result.Suspension is { } suspension)
            return Suspended(request, submission, suspension);
        var problem = result.Status switch
        {
            WebSignInStatus.Succeeded => null,
            WebSignInStatus.RequiresTwoFactor => null,
            WebSignInStatus.LockedOut => "Too many failed attempts: the account is locked for a while.",
            WebSignInStatus.Throttled => $"Slow down — try again in {Math.Ceiling(result.RetryAfter?.TotalSeconds ?? 30)} seconds.",
            WebSignInStatus.NotAllowed => "This account cannot sign in (disabled, or the e-mail address is not confirmed yet).",
            WebSignInStatus.RequiresPasswordChange => "This account must choose a new password: use \"Forgot password\".",
            _ => "Wrong user name or password."
        };
        if (problem is not null)
            return submission.AddError(string.Empty, problem).Invalid(problem, LoginTemplate, LoginModel(request));

        return result.Status == WebSignInStatus.RequiresTwoFactor
            ? FormResult.Success(redirect: "/account/two-factor?returnUrl=" + Uri.EscapeDataString(next))
            : FormResult.Success(redirect: next);
    }

    /// <summary>The themed page for a plain form post; a form message for the WebLogic client.</summary>
    private WebResult Suspended(WebRequestContext request, WebFormSubmission<LoginForm> submission, WebSuspension suspension)
    {
        if (!request.IsWebLogicClient)
            return WebResult.AccountSuspended(suspension);
        var message = "This account is suspended"
            + (suspension.IsIndefinite ? "." : $" until {suspension.Until.UtcDateTime:yyyy-MM-dd HH:mm} UTC.")
            + (suspension.Reason is { Length: > 0 } reason ? " Reason: " + reason : "");
        return submission.AddError(string.Empty, message).Invalid(message, LoginTemplate, LoginModel(request));
    }

    private async ValueTask<WebResult> ShowTwoFactorAsync(WebRequestContext request, CancellationToken cancellationToken)
    {
        if (!await request.Auth.IsSecondFactorPendingAsync(cancellationToken).ConfigureAwait(false))
            return Redirect("/account/login");
        request.Forms.Prefill(new TwoFactorForm { ReturnUrl = WebRequestAuth.GetSafeReturnUrl(request.GetQuery("returnUrl")) });
        return WebResult.Page(TwoFactorTemplate, null, PageModels.Meta("Two-factor sign-in"));
    }

    private async ValueTask<WebResult> TwoFactorAsync(WebRequestContext request, CancellationToken cancellationToken)
    {
        var submission = await request.Forms.BindAsync<TwoFactorForm>(cancellationToken).ConfigureAwait(false);
        if (!submission.IsValid)
            return submission.Invalid(fallbackTemplate: TwoFactorTemplate);
        var result = await request.Auth.CompleteTwoFactorAsync(submission.Model.Code!, cancellationToken).ConfigureAwait(false);
        if (result.Succeeded)
            return FormResult.Success(redirect: WebRequestAuth.GetSafeReturnUrl(submission.Model.ReturnUrl));
        if (!await request.Auth.IsSecondFactorPendingAsync(cancellationToken).ConfigureAwait(false))
            return FormResult.Success(redirect: "/account/login");
        return submission.AddError(nameof(TwoFactorForm.Code), "That code is not valid (or was already used).").Invalid(fallbackTemplate: TwoFactorTemplate);
    }

    private async ValueTask<WebResult> ShowRecoveryCodeAsync(WebRequestContext request, CancellationToken cancellationToken) =>
        await request.Auth.IsSecondFactorPendingAsync(cancellationToken).ConfigureAwait(false)
            ? WebResult.Page(RecoveryTemplate, null, PageModels.Meta("Use a recovery code"))
            : Redirect("/account/login");

    private async ValueTask<WebResult> RecoveryCodeAsync(WebRequestContext request, CancellationToken cancellationToken)
    {
        var submission = await request.Forms.BindAsync<RecoveryCodeForm>(cancellationToken).ConfigureAwait(false);
        if (!submission.IsValid)
            return submission.Invalid(fallbackTemplate: RecoveryTemplate);
        var result = await request.Auth.RedeemRecoveryCodeAsync(submission.Model.Code!, cancellationToken).ConfigureAwait(false);
        return result.Succeeded
            ? FormResult.Success(redirect: "/account?recovered=1")
            : submission.AddError(nameof(RecoveryCodeForm.Code), "That recovery code is not valid.").Invalid(fallbackTemplate: RecoveryTemplate);
    }

    private static async ValueTask<WebResult> LogoutAsync(WebRequestContext request, CancellationToken cancellationToken)
    {
        await request.Auth.SignOutAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        return Redirect("/");
    }

    private ValueTask<WebResult> ShowRegisterAsync(WebRequestContext request, CancellationToken cancellationToken) =>
        ValueTask.FromResult(!_config.AllowRegistration
            ? WebResult.Status(404)
            : request.IsAuthenticated ? Redirect("/account") : WebResult.Page(RegisterTemplate, null, PageModels.Meta("Create an account")));

    private async ValueTask<WebResult> RegisterAsync(WebRequestContext request, CancellationToken cancellationToken)
    {
        if (!_config.AllowRegistration)
            return WebResult.Status(404);
        var submission = await request.Forms.BindAsync<RegisterForm>(cancellationToken).ConfigureAwait(false);
        if (!submission.IsValid)
            return submission.Invalid(fallbackTemplate: RegisterTemplate);

        var created = await _accounts.RegisterAsync(new WebRegistration
        {
            UserName = submission.Model.UserName!,
            Email = submission.Model.Email,
            Password = submission.Model.Password,
            Groups = ["members"]
        }, cancellationToken).ConfigureAwait(false);
        if (created.IsFailure)
            return submission.AddError(string.Empty, created.Error!.Message).Invalid(created.Error.Message, RegisterTemplate);

        await request.Auth.SignInAsync(created.Value!, ["pwd"], cancellationToken: cancellationToken).ConfigureAwait(false);
        return FormResult.Success("Welcome aboard!", "/account?welcome=1");
    }
}
