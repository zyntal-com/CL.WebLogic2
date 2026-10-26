using CL.WebLogic.Security.Audit;
using CL.WebLogic.Security.Authorization;
using CL.WebLogic.Security.Identity;
using CL.WebLogic.Security.Pipeline;
using CL.WebLogic.Security.RateLimiting;
using CL.WebLogic.Security.Rules;
using CL.WebLogic.Security.Sessions;
using CL.WebLogic.Security.Tenancy;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CL.WebLogic.Hosting.Services;

/// <summary>Security registrations on <see cref="WebLogicBuilder"/>.</summary>
public static class WebLogicSecurityBuilderExtensions
{
    /// <summary>Stores sessions in the app's store (replaces the in-memory development store).</summary>
    public static WebLogicBuilder UseSessionStore<TStore>(this WebLogicBuilder builder) where TStore : class, IWebSessionStore =>
        Replace<IWebSessionStore, TStore>(builder);

    /// <summary>Uses a shared rate-limit store (required for consistent limits across a cluster).</summary>
    public static WebLogicBuilder UseRateLimitStore<TStore>(this WebLogicBuilder builder) where TStore : class, IWebRateLimitStore =>
        Replace<IWebRateLimitStore, TStore>(builder);

    /// <summary>Persists audit entries through the app's sink.</summary>
    public static WebLogicBuilder UseAuditSink<TSink>(this WebLogicBuilder builder) where TSink : class, IWebAuditSink =>
        Replace<IWebAuditSink, TSink>(builder);

    /// <summary>Resolves permissions from the app's data (e.g. group → permission mappings).</summary>
    public static WebLogicBuilder UsePermissionResolver<TResolver>(this WebLogicBuilder builder) where TResolver : class, IWebPermissionResolver =>
        Replace<IWebPermissionResolver, TResolver>(builder);

    /// <summary>Enables tenancy with the given resolver.</summary>
    public static WebLogicBuilder UseTenantResolver<TResolver>(this WebLogicBuilder builder) where TResolver : class, IWebTenantResolver =>
        Replace<IWebTenantResolver, TResolver>(builder);

    /// <summary>Adds an authentication handler to the chain (API keys, tokens, …).</summary>
    public static WebLogicBuilder AddAuthenticationHandler<THandler>(this WebLogicBuilder builder) where THandler : class, IWebAuthenticationHandler
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IWebAuthenticationHandler, THandler>());
        return builder;
    }

    /// <summary>Adds a custom security stage (validated for name uniqueness and built-in order at startup).</summary>
    public static WebLogicBuilder AddSecurityStage<TStage>(this WebLogicBuilder builder) where TStage : class, IWebSecurityStage
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IWebSecurityStage, TStage>());
        return builder;
    }

    /// <summary>
    /// Adds app-managed IP rules (bans, allow-lists, trusted addresses) merged with
    /// <c>Security.IpRules</c>. Inject <see cref="IWebSecurityRulesInvalidator"/> to apply edits at once.
    /// </summary>
    public static WebLogicBuilder AddIpRuleProvider<TProvider>(this WebLogicBuilder builder) where TProvider : class, IWebIpRuleProvider
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IWebIpRuleProvider, TProvider>());
        return builder;
    }

    /// <summary>Adds app-managed DNS blocklist zones for the reputation stage (replacing the CL.NetUtils services when non-empty).</summary>
    public static WebLogicBuilder AddReputationListProvider<TProvider>(this WebLogicBuilder builder) where TProvider : class, IWebReputationListProvider
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IWebReputationListProvider, TProvider>());
        return builder;
    }

    /// <summary>Registers a named authorization policy (routes use <c>.RequirePolicy(name)</c>). Policies always require authentication.</summary>
    public static WebLogicBuilder AddAuthorizationPolicy(this WebLogicBuilder builder, string name, Action<WebAuthorizationPolicyBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);
        var policy = new WebAuthorizationPolicyBuilder();
        configure(policy);
        builder.Services.AddSingleton(policy.Build(name));
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
