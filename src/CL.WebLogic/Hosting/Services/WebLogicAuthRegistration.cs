using CL.WebLogic.Auth.Accounts;
using CL.WebLogic.Auth.ApiKeys;
using CL.WebLogic.Auth.External;
using CL.WebLogic.Auth.Handlers;
using CL.WebLogic.Auth.Notifications;
using CL.WebLogic.Auth.Passwords;
using CL.WebLogic.Auth.Sessions;
using CL.WebLogic.Auth.SignIn;
using CL.WebLogic.Auth.Throttling;
using CL.WebLogic.Auth.Tokens;
using CL.WebLogic.Auth.TwoFactor;
using CL.WebLogic.Auth.Users;
using CL.WebLogic.Security.Identity;
using CL.WebLogic.Security.Sessions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CL.WebLogic.Hosting.Services;

/// <summary>
/// Registers the auth flows. Persistence defaults are "null" stores (nothing signs in, nothing is
/// stored) so apps without accounts need nothing; apps with accounts register their stores, or
/// <c>web.UseInMemoryAccounts()</c> while developing.
/// </summary>
internal static class WebLogicAuthRegistration
{
    public static void AddAuth(IServiceCollection services)
    {
        // App-supplied persistence (null defaults)
        services.TryAddSingleton<IWebUserStore, NullWebUserStore>();
        services.TryAddSingleton<IWebApiKeyStore, NullWebApiKeyStore>();
        services.TryAddSingleton<IWebRecoveryCodeStore, NullRecoveryCodeStore>();
        services.TryAddSingleton<IWebSecurityNotifier, NullSecurityNotifier>();

        // Per-node defaults (replace for clusters)
        services.TryAddSingleton<IWebOneTimeUseStore, InMemoryOneTimeUseStore>();
        services.TryAddSingleton<IWebLoginThrottle, InMemoryLoginThrottle>();

        // Primitives
        services.TryAddSingleton<IWebPasswordHasher, Pbkdf2PasswordHasher>();
        services.TryAddSingleton<IWebPasswordPolicy, WebPasswordPolicy>();
        services.TryAddSingleton<IWebTotpProvider, CodeLogicTotpProvider>();
        services.TryAddSingleton<IWebSecurityTokenService, WebSecurityTokenService>();
        services.TryAddSingleton<WebNotificationDispatcher>();
        services.AddHostedService<SecurityNotificationService>();

        // Flows
        services.TryAddSingleton<WebLockoutService>();
        services.TryAddSingleton<WebAttemptGate>();
        services.TryAddSingleton<IWebCredentialValidator, WebCredentialValidator>();
        services.TryAddSingleton<WebTwoFactorService>();
        services.TryAddSingleton<WebAccountService>();
        services.TryAddSingleton<WebAuthService>();
        services.TryAddSingleton<WebApiKeyService>();
        services.TryAddSingleton<WebExternalLoginFlow>();

        // Authentication chain additions
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IWebAuthenticationHandler, TrustedHeaderAuthenticationHandler>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IWebAuthenticationHandler, ApiKeyAuthenticationHandler>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IWebSessionValidator, SecurityStampSessionValidator>());
    }
}
