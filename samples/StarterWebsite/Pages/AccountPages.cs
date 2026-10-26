using CL.WebLogic.Auth.Accounts;
using CL.WebLogic.Auth.ApiKeys;
using CL.WebLogic.Auth.External;
using CL.WebLogic.Auth.TwoFactor;
using CL.WebLogic.Results;
using CL.WebLogic.Routing;
using CL.WebLogic.Routing.Services;
using CL.WebLogic.Templates.Rendering;
using StarterWebsite.Configuration;

namespace StarterWebsite.Pages;

/// <summary>
/// The account area: sign-in (password, two-factor, recovery codes, Steam), registration, password
/// reset and e-mail verification, and the profile (password, authenticator, API keys). Everything
/// security-relevant is done by the toolkit (<c>request.Auth</c>, <see cref="WebAccountService"/>,
/// <see cref="WebTwoFactorService"/>, <see cref="WebApiKeyService"/>); these handlers are UI.
/// </summary>
public sealed partial class AccountPages : IWebContributor
{
    private readonly StarterConfig _config;
    private readonly WebAccountService _accounts;
    private readonly WebTwoFactorService _twoFactor;
    private readonly WebApiKeyService _apiKeys;
    private readonly WebExternalLoginFlow _external;
    private readonly IWebTemplateRenderer _templates;

    /// <summary>Creates the contributor.</summary>
    public AccountPages(StarterConfig config, WebAccountService accounts, WebTwoFactorService twoFactor, WebApiKeyService apiKeys,
        WebExternalLoginFlow external, IWebTemplateRenderer templates)
    {
        _config = config;
        _accounts = accounts;
        _twoFactor = twoFactor;
        _apiKeys = apiKeys;
        _external = external;
        _templates = templates;
    }

    /// <inheritdoc/>
    public Task ContributeAsync(WebContributionBuilder builder, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(builder);
        var account = builder.MapGroup("/account");

        // Signing in and out (anonymous; brute force is slowed by the "login" rate policy and lockout).
        account.MapPage("/login", ShowLoginAsync).AllowAnonymous();
        account.MapPage("/login", LoginAsync, "POST").AllowAnonymous().WithRateLimit("login");
        account.MapPage("/two-factor", ShowTwoFactorAsync).AllowAnonymous();
        account.MapPage("/two-factor", TwoFactorAsync, "POST").AllowAnonymous().WithRateLimit("login");
        account.MapPage("/recovery-code", ShowRecoveryCodeAsync).AllowAnonymous();
        account.MapPage("/recovery-code", RecoveryCodeAsync, "POST").AllowAnonymous().WithRateLimit("login");
        account.MapPage("/logout", LogoutAsync, "POST").AllowAnonymous();
        account.MapPage("/external/{provider}", ExternalChallengeAsync).AllowAnonymous();
        account.MapPage("/external/{provider}/callback", ExternalCallbackAsync).AllowAnonymous();

        // Account lifecycle.
        account.MapPage("/register", ShowRegisterAsync).AllowAnonymous();
        account.MapPage("/register", RegisterAsync, "POST").AllowAnonymous().WithRateLimit("forms");
        account.MapPage("/forgot-password", ShowForgotPasswordAsync).AllowAnonymous();
        account.MapPage("/forgot-password", ForgotPasswordAsync, "POST").AllowAnonymous().WithRateLimit("forms");
        account.MapPage("/reset-password", ShowResetPasswordAsync).AllowAnonymous();
        account.MapPage("/reset-password", ResetPasswordAsync, "POST").AllowAnonymous().WithRateLimit("forms");
        account.MapPage("/verify-email", VerifyEmailAsync).AllowAnonymous();

        // The profile (signed-in users only: the default policy).
        builder.MapPage("/account", ProfileAsync).WithName("account");
        account.MapPage("/resend-verification", ResendVerificationAsync, "POST").WithRateLimit("forms");
        account.MapPage("/password", ChangePasswordAsync, "POST");
        account.MapPage("/sign-out-everywhere", SignOutEverywhereAsync, "POST");
        account.MapPage("/sessions/{id}/revoke", RevokeSessionAsync, "POST");
        account.MapPage("/two-factor/setup", ShowTwoFactorSetupAsync);
        account.MapPage("/two-factor/setup", TwoFactorSetupAsync, "POST");
        account.MapPage("/two-factor/disable", DisableTwoFactorAsync, "POST");
        account.MapPage("/recovery-codes", RegenerateRecoveryCodesAsync, "POST");
        account.MapPage("/api-keys", CreateApiKeyAsync, "POST");
        account.MapPage("/api-keys/{id}/revoke", RevokeApiKeyAsync, "POST");
        return Task.CompletedTask;
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1859", Justification = "Route handlers return WebResult.")]
    private static WebResult Redirect(string url) => WebResult.Redirect(url);
}
