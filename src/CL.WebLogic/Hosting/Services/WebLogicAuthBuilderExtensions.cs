using CL.WebLogic.Auth.ApiKeys;
using CL.WebLogic.Auth.External;
using CL.WebLogic.Auth.Handlers;
using CL.WebLogic.Auth.Notifications;
using CL.WebLogic.Auth.Passwords;
using CL.WebLogic.Auth.SignIn;
using CL.WebLogic.Auth.Throttling;
using CL.WebLogic.Auth.Tokens;
using CL.WebLogic.Auth.TwoFactor;
using CL.WebLogic.Auth.Users;
using CL.WebLogic.Security.Identity;
using CL.WebLogic.Security.Tenancy;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CL.WebLogic.Hosting.Services;

/// <summary>Plugs the app's account persistence and auth options into WebLogic.</summary>
public static class WebLogicAuthBuilderExtensions
{
    /// <summary>Stores users in the app's store. Implement <see cref="IWebUserLockoutStore"/>, <see cref="IWebUserEmailStore"/> and <see cref="IWebUserExternalLoginStore"/> on it as needed.</summary>
    public static WebLogicBuilder UseUserStore<TStore>(this WebLogicBuilder builder) where TStore : class, IWebUserStore =>
        Replace<IWebUserStore, TStore>(builder);

    /// <summary>Stores API keys in the app's store.</summary>
    public static WebLogicBuilder UseApiKeyStore<TStore>(this WebLogicBuilder builder) where TStore : class, IWebApiKeyStore =>
        Replace<IWebApiKeyStore, TStore>(builder);

    /// <summary>Stores recovery codes in the app's store.</summary>
    public static WebLogicBuilder UseRecoveryCodeStore<TStore>(this WebLogicBuilder builder) where TStore : class, IWebRecoveryCodeStore =>
        Replace<IWebRecoveryCodeStore, TStore>(builder);

    /// <summary>Uses a shared one-time-use store (needed for replay protection across a cluster).</summary>
    public static WebLogicBuilder UseOneTimeUseStore<TStore>(this WebLogicBuilder builder) where TStore : class, IWebOneTimeUseStore =>
        Replace<IWebOneTimeUseStore, TStore>(builder);

    /// <summary>Uses another login throttle (e.g. shared across a cluster).</summary>
    public static WebLogicBuilder UseLoginThrottle<TThrottle>(this WebLogicBuilder builder) where TThrottle : class, IWebLoginThrottle =>
        Replace<IWebLoginThrottle, TThrottle>(builder);

    /// <summary>Uses another password hasher.</summary>
    public static WebLogicBuilder UsePasswordHasher<THasher>(this WebLogicBuilder builder) where THasher : class, IWebPasswordHasher =>
        Replace<IWebPasswordHasher, THasher>(builder);

    /// <summary>Verifies an additional (legacy) password hash format; such hashes are upgraded at the next sign-in.</summary>
    public static WebLogicBuilder AddPasswordAlgorithm<TAlgorithm>(this WebLogicBuilder builder) where TAlgorithm : class, IWebPasswordAlgorithm
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IWebPasswordAlgorithm, TAlgorithm>());
        return builder;
    }

    /// <summary>Uses another password policy.</summary>
    public static WebLogicBuilder UsePasswordPolicy<TPolicy>(this WebLogicBuilder builder) where TPolicy : class, IWebPasswordPolicy =>
        Replace<IWebPasswordPolicy, TPolicy>(builder);

    /// <summary>Uses another credential validator (e.g. LDAP).</summary>
    public static WebLogicBuilder UseCredentialValidator<TValidator>(this WebLogicBuilder builder) where TValidator : class, IWebCredentialValidator =>
        Replace<IWebCredentialValidator, TValidator>(builder);

    /// <summary>Uses another TOTP implementation.</summary>
    public static WebLogicBuilder UseTotpProvider<TProvider>(this WebLogicBuilder builder) where TProvider : class, IWebTotpProvider =>
        Replace<IWebTotpProvider, TProvider>(builder);

    /// <summary>Delivers security e-mails through the app's notifier.</summary>
    public static WebLogicBuilder UseSecurityNotifier<TNotifier>(this WebLogicBuilder builder) where TNotifier : class, IWebSecurityNotifier =>
        Replace<IWebSecurityNotifier, TNotifier>(builder);

    /// <summary>Sends security e-mails through CL.Mail (the Mail library must be loaded).</summary>
    public static WebLogicBuilder UseMailNotifications(this WebLogicBuilder builder) =>
        Replace<IWebSecurityNotifier, MailSecurityNotifier>(builder);

    /// <summary>Writes security e-mails to the development outbox (.eml files). Development only.</summary>
    public static WebLogicBuilder UseOutboxNotifications(this WebLogicBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Services.TryAddSingleton<OutboxSecurityNotifier>();
        builder.Services.Replace(ServiceDescriptor.Singleton<IWebSecurityNotifier>(sp => sp.GetRequiredService<OutboxSecurityNotifier>()));
        return builder;
    }

    /// <summary>
    /// In-memory users, API keys, recovery codes and the e-mail outbox, for development and tests.
    /// Refused outside Development unless <c>Security.AllowDevelopmentStores</c> is set.
    /// </summary>
    public static WebLogicBuilder UseInMemoryAccounts(this WebLogicBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        var services = builder.Services;
        services.TryAddSingleton<InMemoryWebUserStore>();
        services.Replace(ServiceDescriptor.Singleton<IWebUserStore>(sp => sp.GetRequiredService<InMemoryWebUserStore>()));
        services.TryAddSingleton<InMemoryWebApiKeyStore>();
        services.Replace(ServiceDescriptor.Singleton<IWebApiKeyStore>(sp => sp.GetRequiredService<InMemoryWebApiKeyStore>()));
        services.TryAddSingleton<InMemoryRecoveryCodeStore>();
        services.Replace(ServiceDescriptor.Singleton<IWebRecoveryCodeStore>(sp => sp.GetRequiredService<InMemoryRecoveryCodeStore>()));
        return builder.UseOutboxNotifications();
    }

    /// <summary>Accepts bearer tokens validated by <typeparamref name="TValidator"/>.</summary>
    public static WebLogicBuilder AddBearerTokens<TValidator>(this WebLogicBuilder builder) where TValidator : class, IWebBearerTokenValidator
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Services.Replace(ServiceDescriptor.Singleton<IWebBearerTokenValidator, TValidator>());
        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IWebAuthenticationHandler, BearerTokenAuthenticationHandler>());
        return builder;
    }

    /// <summary>
    /// Uses an ASP.NET authentication scheme the app added (Negotiate, JwtBearer, certificate) as a
    /// WebLogic identity source. <paramref name="ambient"/>: the browser sends the credential
    /// automatically (Negotiate, client certificates), so CSRF protection applies.
    /// </summary>
    public static WebLogicBuilder AddAspNetAuthenticationScheme(this WebLogicBuilder builder, string scheme, bool ambient, int order = 400)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Services.AddSingleton<IWebAuthenticationHandler>(new AspNetSchemeAuthenticationHandler(scheme, ambient, order));
        return builder;
    }

    /// <summary>Adds an external login provider (see <see cref="WebExternalLoginFlow"/>).</summary>
    public static WebLogicBuilder AddExternalLoginProvider<TProvider>(this WebLogicBuilder builder) where TProvider : class, IWebExternalLoginProvider
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IWebExternalLoginProvider, TProvider>());
        return builder;
    }

    /// <summary>Resolves the tenant from the host name (host → tenant id).</summary>
    public static WebLogicBuilder UseHostTenants(this WebLogicBuilder builder, IReadOnlyDictionary<string, string> hostToTenant)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Services.Replace(ServiceDescriptor.Singleton<IWebTenantResolver>(new HostWebTenantResolver(hostToTenant)));
        return builder;
    }

    private static WebLogicBuilder Replace<TService, TImplementation>(WebLogicBuilder builder)
        where TService : class
        where TImplementation : class, TService
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Services.Replace(ServiceDescriptor.Singleton<TService, TImplementation>());
        return builder;
    }
}
