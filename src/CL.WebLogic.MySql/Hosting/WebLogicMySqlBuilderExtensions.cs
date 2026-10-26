using CL.WebLogic.Auth.ApiKeys;
using CL.WebLogic.Auth.Passkeys;
using CL.WebLogic.Auth.Throttling;
using CL.WebLogic.Auth.Tokens;
using CL.WebLogic.Auth.TwoFactor;
using CL.WebLogic.Auth.Users;
using CL.WebLogic.Dashboard.Services;
using CL.WebLogic.Hosting.Services;
using CL.WebLogic.MySql.Entities;
using CL.WebLogic.MySql.Models;
using CL.WebLogic.MySql.Services;
using CL.WebLogic.MySql.Stores;
using CL.WebLogic.Security.Audit;
using CL.WebLogic.Security.RateLimiting;
using CL.WebLogic.Security.Sessions;
using CL.WebLogic.Widgets.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace CL.WebLogic.MySql.Hosting;

/// <summary>Registers the MySQL stores.</summary>
public static class WebLogicMySqlBuilderExtensions
{
    /// <summary>
    /// Uses MySQL (CL.MySQL2 connection <paramref name="connectionId"/>) for every WebLogic store,
    /// or the ones in <see cref="WebMySqlOptions.Stores"/>. Tables (<c>wl_*</c>) are created at
    /// startup and expired rows are swept. CL.MySQL2 must be loaded before WebLogic.
    /// </summary>
    public static WebLogicBuilder UseMySqlStores(this WebLogicBuilder builder, string connectionId = "Default", Action<WebMySqlOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionId);
        var options = new WebMySqlOptions { ConnectionId = connectionId };
        configure?.Invoke(options);
        if (options.SweepInterval <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(configure), "SweepInterval must be positive.");

        var services = builder.Services;
        services.AddSingleton(options);
        services.TryAddSingleton<WebMySqlDatabase>();

        var tables = new List<Type>();
        void Use<TContract, TStore>(WebMySqlStores flag, params Type[] rows) where TContract : class where TStore : class, TContract
        {
            if (!options.Stores.HasFlag(flag))
                return;
            services.TryAddSingleton<TStore>();
            services.Replace(ServiceDescriptor.Singleton<TContract>(sp => sp.GetRequiredService<TStore>()));
            tables.AddRange(rows);
        }

        Use<IWebUserStore, MySqlWebUserStore>(WebMySqlStores.Users, typeof(UserRow), typeof(UserLoginRow));
        Use<IWebSessionStore, MySqlWebSessionStore>(WebMySqlStores.Sessions, typeof(SessionRow));
        Use<IWebApiKeyStore, MySqlWebApiKeyStore>(WebMySqlStores.ApiKeys, typeof(ApiKeyRow));
        Use<IWebRecoveryCodeStore, MySqlWebRecoveryCodeStore>(WebMySqlStores.RecoveryCodes, typeof(RecoveryCodeRow));
        Use<IWebOneTimeUseStore, MySqlWebOneTimeUseStore>(WebMySqlStores.OneTimeUse, typeof(OneTimeUseRow));
        Use<IWebLoginThrottle, MySqlWebLoginThrottle>(WebMySqlStores.LoginThrottle, typeof(LoginThrottleRow));
        Use<IWebRateLimitStore, MySqlWebRateLimitStore>(WebMySqlStores.RateLimits, typeof(RateLimitRow));
        Use<IWebAuditSink, MySqlWebAuditSink>(WebMySqlStores.Audit, typeof(AuditRow));
        Use<IWebWidgetSettingsStore, MySqlWebWidgetSettingsStore>(WebMySqlStores.WidgetSettings, typeof(WidgetSettingsRow));
        Use<IWebDashboardLayoutStore, MySqlWebDashboardLayoutStore>(WebMySqlStores.Dashboards, typeof(DashboardRow));
        Use<IWebPasskeyStore, MySqlWebPasskeyStore>(WebMySqlStores.Passkeys, typeof(PasskeyRow));

        if (options.SyncSchemaOnStart && tables.Count > 0)
            AddTables(services, new WebMySqlTables(connectionId, tables));
        services.AddHostedService<WebMySqlSweepService>();
        return builder;
    }

    /// <summary>
    /// Adds a group of tables to the startup schema sync. The sync is the first hosted service, so
    /// the tables exist before anything else starts (DataProtection reads the key ring at startup).
    /// </summary>
    internal static void AddTables(IServiceCollection services, WebMySqlTables tables)
    {
        services.AddSingleton(tables);
        if (!services.Any(d => d.ServiceType == typeof(IHostedService) && d.ImplementationType == typeof(WebMySqlSchemaService)))
            services.Insert(0, ServiceDescriptor.Singleton<IHostedService, WebMySqlSchemaService>());
    }
}
