using CL.WebLogic.Auth.Accounts;
using CL.WebLogic.Auth.SignIn;
using CL.WebLogic.Results;
using CL.WebLogic.Routing;
using CL.WebLogic.Routing.Services;
using CL.WebLogic.Runtime.Models;
using MiniBlog.Forms;

namespace MiniBlog.Pages;

/// <summary>Editor sign-in, sign-out and password reset (there is no public registration).</summary>
public sealed class AccountPages : IWebContributor
{
    private const string LoginTemplate = "templates/login";
    private readonly WebAccountService _accounts;

    /// <summary>Creates the contributor.</summary>
    public AccountPages(WebAccountService accounts) => _accounts = accounts;

    /// <inheritdoc/>
    public Task ContributeAsync(WebContributionBuilder builder, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.MapPage("/login", (r, _) =>
        {
            r.Forms.Prefill(new LoginForm { ReturnUrl = WebRequestAuth.GetSafeReturnUrl(r.GetQuery("returnUrl")) });
            return ValueTask.FromResult<WebResult>(WebResult.Page(LoginTemplate, new { Reset = r.GetQuery("reset") == "1" }, new WebPageMeta { Title = "Sign in" }));
        }).AllowAnonymous();
        builder.MapPage("/login", LoginAsync, "POST").AllowAnonymous().WithRateLimit("login");
        builder.MapPage("/logout", async (r, ct) =>
        {
            await r.Auth.SignOutAsync(cancellationToken: ct).ConfigureAwait(false);
            return WebResult.Redirect("/");
        }, "POST").AllowAnonymous();
        builder.MapPage("/forgot-password", (r, _) => ValueTask.FromResult<WebResult>(WebResult.Page("templates/forgot-password", new { Sent = r.GetQuery("sent") == "1" }, new WebPageMeta { Title = "Forgot password" })))
            .AllowAnonymous();
        builder.MapPage("/forgot-password", ForgotAsync, "POST").AllowAnonymous().WithRateLimit("forms");
        builder.MapPage("/account/reset-password", (r, _) =>
        {
            r.Forms.Prefill(new ResetPasswordForm { Token = r.GetQuery("token") });
            return ValueTask.FromResult<WebResult>(WebResult.Page("templates/reset-password", null, new WebPageMeta { Title = "New password" }));
        }).AllowAnonymous();
        builder.MapPage("/account/reset-password", ResetAsync, "POST").AllowAnonymous().WithRateLimit("forms");
        return Task.CompletedTask;
    }

    private static async ValueTask<WebResult> LoginAsync(WebRequestContext request, CancellationToken cancellationToken)
    {
        var submission = await request.Forms.BindAsync<LoginForm>(cancellationToken).ConfigureAwait(false);
        var model = new { Reset = false };
        if (!submission.IsValid)
            return submission.Invalid(fallbackTemplate: LoginTemplate, fallbackModel: model);
        var result = await request.Auth.PasswordSignInAsync(submission.Model.Login!, submission.Model.Password!, cancellationToken).ConfigureAwait(false);
        if (result.Status != WebSignInStatus.Succeeded)
        {
            var message = result.Status switch
            {
                WebSignInStatus.LockedOut => "Too many failed attempts: try again later.",
                WebSignInStatus.Throttled => "Slow down and try again in a moment.",
                _ => "Wrong user name or password."
            };
            return submission.AddError(string.Empty, message).Invalid(message, LoginTemplate, model);
        }
        return FormResult.Success(redirect: WebRequestAuth.GetSafeReturnUrl(submission.Model.ReturnUrl) is "/" ? "/admin" : WebRequestAuth.GetSafeReturnUrl(submission.Model.ReturnUrl));
    }

    private async ValueTask<WebResult> ForgotAsync(WebRequestContext request, CancellationToken cancellationToken)
    {
        var submission = await request.Forms.BindAsync<ForgotPasswordForm>(cancellationToken).ConfigureAwait(false);
        if (!submission.IsValid)
            return submission.Invalid(fallbackTemplate: "templates/forgot-password", fallbackModel: new { Sent = false });
        await _accounts.RequestPasswordResetAsync(submission.Model.Email!, cancellationToken).ConfigureAwait(false);
        return FormResult.Success("If an account uses that address, a reset link is on its way.", "/forgot-password?sent=1");
    }

    private async ValueTask<WebResult> ResetAsync(WebRequestContext request, CancellationToken cancellationToken)
    {
        var submission = await request.Forms.BindAsync<ResetPasswordForm>(cancellationToken).ConfigureAwait(false);
        if (!submission.IsValid)
            return submission.Invalid(fallbackTemplate: "templates/reset-password");
        var reset = await _accounts.ResetPasswordAsync(submission.Model.Token!, submission.Model.NewPassword!, cancellationToken).ConfigureAwait(false);
        return reset.IsSuccess
            ? FormResult.Success("Password changed.", "/login?reset=1")
            : submission.AddError(string.Empty, "This link is invalid, expired or already used.").Invalid(fallbackTemplate: "templates/reset-password");
    }
}
