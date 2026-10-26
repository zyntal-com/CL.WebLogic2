using CL.WebLogic.Auth.Passkeys;
using CL.WebLogic.Auth.SignIn;
using CL.WebLogic.Auth.Users;
using CL.WebLogic.Results;
using CL.WebLogic.Routing.Services;
using CL.WebLogic.Runtime.Models;
using CodeLogic.Core.Results;

namespace StarterWebsite.Pages;

/// <summary>
/// Passkeys: JSON endpoints for the client's <c>data-wl-passkey</c> buttons (sign-in, second factor,
/// registration) and a plain form to remove one. The toolkit verifies everything; this is wiring.
/// </summary>
public sealed partial class AccountPages
{
    private static void MapPasskeys(WebRouteGroupBuilder account)
    {
        account.MapApi("/passkeys/sign-in/begin", async (r, ct) => Challenge(await r.Auth.BeginPasskeySignInAsync(ct)), "POST").AllowAnonymous().WithRateLimit("login");
        account.MapApi("/passkeys/sign-in/finish", PasskeySignInAsync, "POST").AllowAnonymous().WithRateLimit("login");
        account.MapApi("/passkeys/two-factor/begin", async (r, ct) => Challenge(await r.Auth.BeginPasskeySecondFactorAsync(ct)), "POST").AllowAnonymous().WithRateLimit("login");
        account.MapApi("/passkeys/two-factor/finish", PasskeySecondFactorAsync, "POST").AllowAnonymous().WithRateLimit("login");
        account.MapApi("/passkeys/register/begin", async (r, ct) => Challenge(await r.Auth.BeginPasskeyRegistrationAsync(ct)), "POST").WithRateLimit("forms");
        account.MapApi("/passkeys/register/finish", RegisterPasskeyAsync, "POST").WithRateLimit("forms");
        account.MapPage("/passkeys/{id}/delete", DeletePasskeyAsync, "POST");
    }

    private static WebResult Challenge(Result<WebPasskeyChallenge> challenge) =>
        challenge.IsSuccess ? WebResult.Json(challenge.Value!) : WebResult.Problem(400, challenge.Error!.Message);

    private static JsonResult Outcome(bool ok, string? message = null, string? redirect = null) =>
        WebResult.Json(new { ok, message, redirect });

    private static async ValueTask<WebResult> PasskeySignInAsync(WebRequestContext request, CancellationToken cancellationToken)
    {
        var body = await request.ReadJsonAsync<WebPasskeyRequest>(cancellationToken).ConfigureAwait(false) ?? new WebPasskeyRequest();
        var result = await request.Auth.PasskeySignInAsync(body, cancellationToken).ConfigureAwait(false);
        return result.Status switch
        {
            WebSignInStatus.Succeeded => Outcome(true, redirect: WebRequestAuth.GetSafeReturnUrl(request.GetQuery("returnUrl"))),
            WebSignInStatus.Suspended => Outcome(false, SuspensionMessage(result.Suspension!)),
            WebSignInStatus.NotAllowed => Outcome(false, "This account cannot sign in."),
            WebSignInStatus.RequiresPasswordChange => Outcome(false, "This account must choose a new password: use \"Forgot password\"."),
            WebSignInStatus.Throttled => Outcome(false, "Slow down — try again in a moment."),
            _ => Outcome(false, "That passkey could not be used here.")
        };
    }

    private static async ValueTask<WebResult> PasskeySecondFactorAsync(WebRequestContext request, CancellationToken cancellationToken)
    {
        var body = await request.ReadJsonAsync<WebPasskeyRequest>(cancellationToken).ConfigureAwait(false) ?? new WebPasskeyRequest();
        var result = await request.Auth.CompleteTwoFactorWithPasskeyAsync(body, cancellationToken).ConfigureAwait(false);
        if (result.Succeeded)
            return Outcome(true, redirect: WebRequestAuth.GetSafeReturnUrl(request.GetQuery("returnUrl")));
        return result.Suspension is { } suspension
            ? Outcome(false, SuspensionMessage(suspension))
            : Outcome(false, "That passkey could not be used.", await request.Auth.IsSecondFactorPendingAsync(cancellationToken).ConfigureAwait(false) ? null : "/account/login");
    }

    private static async ValueTask<WebResult> RegisterPasskeyAsync(WebRequestContext request, CancellationToken cancellationToken)
    {
        var body = await request.ReadJsonAsync<WebPasskeyRequest>(cancellationToken).ConfigureAwait(false) ?? new WebPasskeyRequest();
        var result = await request.Auth.CompletePasskeyRegistrationAsync(body, cancellationToken).ConfigureAwait(false);
        return result.IsSuccess
            ? Outcome(true, $"Passkey \"{result.Value!.Name}\" added.", "/account")
            : Outcome(false, result.Error!.Message);
    }

    private static async ValueTask<WebResult> DeletePasskeyAsync(WebRequestContext request, CancellationToken cancellationToken)
    {
        await request.Auth.DeletePasskeyAsync(request.GetRouteValue("id") ?? "", cancellationToken).ConfigureAwait(false);
        return Redirect("/account");
    }

    private static string SuspensionMessage(WebSuspension suspension) =>
        "This account is suspended" + (suspension.IsIndefinite ? "." : $" until {suspension.Until.UtcDateTime:yyyy-MM-dd HH:mm} UTC.")
        + (suspension.Reason is { Length: > 0 } reason ? " Reason: " + reason : "");
}
