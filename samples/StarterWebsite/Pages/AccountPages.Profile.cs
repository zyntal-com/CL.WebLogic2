using System.Text.Encodings.Web;
using CL.WebLogic.Auth.ApiKeys;
using CL.WebLogic.Results;
using CL.WebLogic.Runtime.Models;
using CL.WebLogic.Templates.Escaping;
using CL.WebLogic.Templates.Rendering;
using StarterWebsite.Forms;

namespace StarterWebsite.Pages;

public sealed partial class AccountPages
{
    private const string ProfileTemplate = "templates/account/profile";

    private async Task<object?> ProfileModelAsync(WebRequestContext request, CancellationToken cancellationToken)
    {
        var user = await request.Auth.GetCurrentUserAsync(cancellationToken).ConfigureAwait(false);
        if (user is null)
            return null;
        var keys = await _apiKeys.ListAsync(user.Id, cancellationToken).ConfigureAwait(false);
        return new
        {
            User = new { user.Id, user.UserName, user.Email, user.EmailConfirmed, Groups = string.Join(", ", user.Groups), user.TwoFactorEnabled, user.CreatedAt },
            RecoveryCodes = user.TwoFactorEnabled ? await _twoFactor.RemainingRecoveryCodesAsync(user, cancellationToken).ConfigureAwait(false) : 0,
            ApiKeys = keys.Where(k => k.RevokedAt is null).Select(k => new
            {
                k.Id, k.Name, k.Prefix, Scopes = string.Join(", ", k.Scopes), k.CreatedAt, k.ExpiresAt, k.LastUsedAt
            }).ToList(),
            Methods = string.Join(", ", request.Identity.AuthenticationMethods),
            Sessions = (await request.Auth.ListSessionsAsync(cancellationToken).ConfigureAwait(false)).Select(s => new
            {
                s.IdHash, Device = s.UserAgent ?? "Unknown browser", s.CreatedAt, s.LastSeenAt, s.Persistent, s.IsCurrent
            }).ToList(),
            Welcome = request.GetQuery("welcome") == "1",
            Sent = request.GetQuery("sent") == "1",
            Changed = request.GetQuery("changed") == "1",
            Recovered = request.GetQuery("recovered") == "1"
        };
    }

    private async ValueTask<WebResult> ProfileAsync(WebRequestContext request, CancellationToken cancellationToken) =>
        await ProfileModelAsync(request, cancellationToken).ConfigureAwait(false) is { } model
            ? WebResult.Page(ProfileTemplate, model, PageModels.Meta("Your account"))
            : Redirect("/account/login");

    private async ValueTask<WebResult> ChangePasswordAsync(WebRequestContext request, CancellationToken cancellationToken)
    {
        var submission = await request.Forms.BindAsync<ChangePasswordForm>(cancellationToken).ConfigureAwait(false);
        if (submission.IsValid)
        {
            var changed = await request.Auth.ChangePasswordAsync(submission.Model.CurrentPassword!, submission.Model.NewPassword!, cancellationToken).ConfigureAwait(false);
            if (changed.IsSuccess)
                return FormResult.Success("Password changed; your other sessions were signed out.", "/account?changed=1");
            submission.AddError(changed.Error!.Code.Contains("current", StringComparison.OrdinalIgnoreCase) ? nameof(ChangePasswordForm.CurrentPassword) : nameof(ChangePasswordForm.NewPassword), changed.Error.Message);
        }
        return submission.Invalid(fallbackTemplate: ProfileTemplate, fallbackModel: await ProfileModelAsync(request, cancellationToken).ConfigureAwait(false));
    }

    private static async ValueTask<WebResult> RevokeSessionAsync(WebRequestContext request, CancellationToken cancellationToken)
    {
        if (request.GetRouteValue("id") is { } id)
            await request.Auth.RevokeSessionAsync(id, cancellationToken).ConfigureAwait(false);
        return Redirect("/account");
    }

    private static async ValueTask<WebResult> SignOutEverywhereAsync(WebRequestContext request, CancellationToken cancellationToken)
    {
        await request.Auth.SignOutAsync(everywhere: true, cancellationToken).ConfigureAwait(false);
        return Redirect("/account/login");
    }

    private async ValueTask<WebResult> ShowTwoFactorSetupAsync(WebRequestContext request, CancellationToken cancellationToken)
    {
        var user = await request.Auth.GetCurrentUserAsync(cancellationToken).ConfigureAwait(false);
        if (user is null || user.TwoFactorEnabled)
            return Redirect("/account");
        var enrollment = _twoFactor.BeginEnrollment(user);
        request.Forms.Prefill(new TwoFactorSetupForm { EnrollmentToken = enrollment.EnrollmentToken });
        // The QR code is a data: image produced on the server, so it is emitted as trusted HTML
        // (templates only allow http(s) URLs in src attributes).
        var qr = enrollment.QrCodeDataUri is null
            ? WebHtml.Empty
            : new WebHtml($"<img class=\"qr\" alt=\"QR code for your authenticator app\" width=\"200\" height=\"200\" src=\"{HtmlEncoder.Default.Encode(enrollment.QrCodeDataUri)}\">");
        return WebResult.Page("templates/account/two-factor-setup", new { Qr = qr, enrollment.Secret }, PageModels.Meta("Set up two-factor"));
    }

    private async ValueTask<WebResult> TwoFactorSetupAsync(WebRequestContext request, CancellationToken cancellationToken)
    {
        var user = await request.Auth.GetCurrentUserAsync(cancellationToken).ConfigureAwait(false);
        var submission = await request.Forms.BindAsync<TwoFactorSetupForm>(cancellationToken).ConfigureAwait(false);
        if (user is null || !submission.IsValid)
            return submission.Invalid();
        var confirmed = await _twoFactor.ConfirmEnrollmentAsync(user, submission.Model.EnrollmentToken!, submission.Model.Code!, cancellationToken).ConfigureAwait(false);
        if (confirmed.IsFailure)
            return submission.AddError(nameof(TwoFactorSetupForm.Code), confirmed.Error!.Message).Invalid();
        await request.Auth.RefreshAsync(cancellationToken).ConfigureAwait(false);
        return await RecoveryCodesResultAsync(request, confirmed.Value!, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<WebResult> DisableTwoFactorAsync(WebRequestContext request, CancellationToken cancellationToken)
    {
        if (await request.Auth.GetCurrentUserAsync(cancellationToken).ConfigureAwait(false) is { } user)
            await _twoFactor.DisableAsync(user, cancellationToken).ConfigureAwait(false);
        await request.Auth.RefreshAsync(cancellationToken).ConfigureAwait(false);
        return Redirect("/account");
    }

    private async ValueTask<WebResult> RegenerateRecoveryCodesAsync(WebRequestContext request, CancellationToken cancellationToken)
    {
        var user = await request.Auth.GetCurrentUserAsync(cancellationToken).ConfigureAwait(false);
        if (user is null || !user.TwoFactorEnabled)
            return Redirect("/account");
        var codes = await _twoFactor.RegenerateRecoveryCodesAsync(user, cancellationToken).ConfigureAwait(false);
        return await RecoveryCodesResultAsync(request, codes, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Recovery codes are shown exactly once: as a page, or swapped into the page for the WebLogic client.</summary>
    private Task<WebResult> RecoveryCodesResultAsync(WebRequestContext request, IReadOnlyList<string> codes, CancellationToken cancellationToken) =>
        OnceAsync(request, "templates/account/recovery-codes", new { Codes = codes }, "Recovery codes", "#two-factor-panel", cancellationToken);

    private async ValueTask<WebResult> CreateApiKeyAsync(WebRequestContext request, CancellationToken cancellationToken)
    {
        var submission = await request.Forms.BindAsync<ApiKeyForm>(cancellationToken).ConfigureAwait(false);
        var user = await request.Auth.GetCurrentUserAsync(cancellationToken).ConfigureAwait(false);
        if (user is null || !submission.IsValid)
            return submission.Invalid(fallbackTemplate: ProfileTemplate, fallbackModel: await ProfileModelAsync(request, cancellationToken).ConfigureAwait(false));

        var created = await _apiKeys.CreateAsync(new WebApiKeyRequest
        {
            Name = submission.Model.Name!,
            UserId = user.Id,
            Scopes = submission.Model.Scopes,
            Groups = user.Groups,
            Lifetime = submission.Model.ExpiresInDays > 0 ? TimeSpan.FromDays(submission.Model.ExpiresInDays) : null
        }, cancellationToken).ConfigureAwait(false);
        return await OnceAsync(request, "templates/account/api-key-created", new { Key = created.PlainTextKey, created.Key.Name },
            "API key created", "#api-key-result", cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<WebResult> RevokeApiKeyAsync(WebRequestContext request, CancellationToken cancellationToken)
    {
        if (request.GetRouteValue("id") is { } id && request.Identity.UserId is { } userId)
            await _apiKeys.RevokeAsync(id, userId, cancellationToken).ConfigureAwait(false);
        return Redirect("/account");
    }

    /// <summary>
    /// A secret shown once. The WebLogic client gets the fragment swapped into <paramref name="target"/>;
    /// a plain form post gets a full page. Nothing is stored in the session or put in a URL.
    /// </summary>
    private async Task<WebResult> OnceAsync(WebRequestContext request, string template, object model, string title, string target, CancellationToken cancellationToken)
    {
        if (!request.IsWebLogicClient)
            return WebResult.Page(template, model, PageModels.Meta(title));
        var html = await _templates.RenderAsync(new TemplateRenderRequest { TemplatePath = template, Model = model, HttpContext = request.HttpContext, IgnoreLayout = true }, cancellationToken).ConfigureAwait(false);
        return FormResult.Success(commands: WebCommand.Replace(target, html));
    }
}
