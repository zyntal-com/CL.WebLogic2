using CL.WebLogic.Auth.Accounts;
using CL.WebLogic.Results;
using CL.WebLogic.Runtime.Models;

namespace StarterWebsite.Pages;

public sealed partial class AccountPages
{
    private async ValueTask<WebResult> ExternalChallengeAsync(WebRequestContext request, CancellationToken cancellationToken)
    {
        var provider = request.GetRouteValue("provider") ?? string.Empty;
        return await _external.ChallengeAsync(request, provider, $"/account/external/{Uri.EscapeDataString(provider)}/callback",
            request.GetQuery("returnUrl"), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Signs in a linked account, links to the signed-in account, or creates a new one.</summary>
    private async ValueTask<WebResult> ExternalCallbackAsync(WebRequestContext request, CancellationToken cancellationToken)
    {
        var outcome = await _external.HandleCallbackAsync(request, request.GetRouteValue("provider") ?? string.Empty, cancellationToken).ConfigureAwait(false);
        if (outcome.IsFailure)
            return WebResult.Page("templates/account/external-failed", new { outcome.Error!.Message }, PageModels.Meta("Sign-in failed"), statusCode: 400);

        var (info, user, returnUrl) = (outcome.Value!.Info, outcome.Value.User, outcome.Value.ReturnUrl);
        if (user is null && await request.Auth.GetCurrentUserAsync(cancellationToken).ConfigureAwait(false) is { } current)
        {
            await _external.LinkAsync(current, info, cancellationToken).ConfigureAwait(false);
            return Redirect("/account");
        }

        if (user is null)
        {
            var created = await _accounts.RegisterAsync(new WebRegistration
            {
                UserName = $"{info.Provider}-{info.ProviderKey}",
                Email = info.EmailVerified ? info.Email : null,
                EmailConfirmed = info.EmailVerified,
                Groups = ["members"],
                SendVerificationEmail = false
            }, cancellationToken).ConfigureAwait(false);
            if (created.IsFailure)
                return WebResult.Page("templates/account/external-failed", new { created.Error!.Message }, PageModels.Meta("Sign-in failed"), statusCode: 409);
            user = created.Value!;
            await _external.LinkAsync(user, info, cancellationToken).ConfigureAwait(false);
        }

        await request.Auth.SignInAsync(user, ["ext:" + info.Provider], cancellationToken).ConfigureAwait(false);
        return Redirect(returnUrl);
    }
}
