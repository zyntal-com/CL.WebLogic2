using CL.WebLogic.Security.Antiforgery;
using CL.WebLogic.Security.Audit;
using CL.WebLogic.Security.Authorization;
using CL.WebLogic.Security.Headers;
using CL.WebLogic.Security.Identity;
using CL.WebLogic.Security.Pipeline;
using CL.WebLogic.Security.RateLimiting;
using CL.WebLogic.Security.Rules;
using CL.WebLogic.Security.Services;
using CL.WebLogic.Security.Sessions;
using CL.WebLogic.Security.Stages;
using CL.WebLogic.Templates.Rendering;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CL.WebLogic.Hosting.Services;

/// <summary>Registers the security pipeline and its default (replaceable) contracts.</summary>
internal static class WebLogicSecurityRegistration
{
    public static void AddSecurity(IServiceCollection services)
    {
        // Guard first, so a misconfigured production host fails before anything else starts.
        services.AddHostedService<DevelopmentStoreGuard>();
        services.TryAddSingleton<ClusterReadinessCheck>();
        services.AddHostedService<ClusterGuard>();

        services.AddDataProtection();
        // Do not advertise the server software (Kestrel adds "Server: Kestrel" after OnStarting runs).
        services.Configure<Microsoft.AspNetCore.Server.Kestrel.Core.KestrelServerOptions>(o => o.AddServerHeader = false);
        services.AddAuthentication(WebLogicAuthenticationHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, WebLogicAuthenticationHandler>(WebLogicAuthenticationHandler.SchemeName, null);
        services.AddAuthorization();

        // Pipeline
        services.TryAddSingleton<WebSecuritySettingsProvider>();
        services.TryAddSingleton<WebSecurityPipeline>();
        services.TryAddSingleton<WebSecurityResponder>();
        services.TryAddSingleton<WebSecurityRules>();
        services.TryAddSingleton<IWebSecurityRulesInvalidator>(sp => sp.GetRequiredService<WebSecurityRules>());
        services.TryAddSingleton<ResponseHardener>();
        services.TryAddSingleton<SecurityContributor>();

        // Stages (built-in names are validated for order at startup)
        AddStage<ForwardedHeadersStage>(services);
        AddStage<HostFilterStage>(services);
        AddStage<HttpsStage>(services);
        AddStage<TenantStage>(services);
        AddStage<IpRulesStage>(services);
        AddStage<ReputationStage>(services);
        AddStage<GlobalRateLimitStage>(services);
        AddStage<AuthenticationStage>(services);
        AddStage<RequestLimitsStage>(services);
        AddStage<RouteRateLimitStage>(services);
        AddStage<AuthorizationStage>(services);
        AddStage<AntiforgeryStage>(services);

        // Identity, sessions, authorization
        services.TryAddSingleton<IWebSessionStore, InMemoryWebSessionStore>();
        services.TryAddSingleton<WebSessionManager>();
        services.AddHostedService<SessionSweeperService>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IWebAuthenticationHandler, SessionCookieAuthenticationHandler>());
        services.TryAddSingleton<IWebPermissionResolver, IdentityPermissionResolver>();
        services.TryAddSingleton<WebPermissionService>();
        services.TryAddSingleton<IWebAuthorizationService, WebAuthorizationService>();

        // Throttling, CSRF, audit
        services.TryAddSingleton<IWebRateLimitStore, InMemoryRateLimitStore>();
        services.TryAddSingleton<WebCsrfTokens>();
        services.TryAddSingleton<IWebCsrfTokens>(sp => sp.GetRequiredService<WebCsrfTokens>());
        services.TryAddSingleton<IWebIpHasher, HmacIpHasher>();
        services.TryAddSingleton<WebAuditLog>();
        services.TryAddSingleton<IWebAuditLog>(sp => sp.GetRequiredService<WebAuditLog>());
        services.TryAddSingleton<IWebAuditSink, NullAuditSink>();
        services.AddHostedService<AuditFlushService>();

        // Template integration
        services.TryAddSingleton<ITemplateCsrfProvider>(sp => sp.GetRequiredService<WebCsrfTokens>());
        services.TryAddSingleton<ITemplateAuthorizer, SecurityTemplateAuthorizer>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<ITemplateRenderPreparer, PermissionTemplatePreparer>());
    }

    private static void AddStage<TStage>(IServiceCollection services) where TStage : class, IWebSecurityStage =>
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IWebSecurityStage, TStage>());
}
