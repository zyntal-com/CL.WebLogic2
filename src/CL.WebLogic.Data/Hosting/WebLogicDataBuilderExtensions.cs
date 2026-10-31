using CL.Data.Mapping;
using CL.WebLogic.Auth.ApiKeys;
using CL.WebLogic.Auth.Passkeys;
using CL.WebLogic.Auth.Throttling;
using CL.WebLogic.Auth.Tokens;
using CL.WebLogic.Auth.TwoFactor;
using CL.WebLogic.Auth.Users;
using CL.WebLogic.Dashboard.Services;
using CL.WebLogic.Data.Entities;
using CL.WebLogic.Data.Models;
using CL.WebLogic.Data.Services;
using CL.WebLogic.Data.Stores;
using CL.WebLogic.Hosting.Services;
using CL.WebLogic.Security.Audit;
using CL.WebLogic.Security.RateLimiting;
using CL.WebLogic.Security.Sessions;
using CL.WebLogic.Widgets.Services;
using CodeLogic.Core.Results;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace CL.WebLogic.Data.Hosting;

/// <summary>Registers the database stores.</summary>
public static class WebLogicDataBuilderExtensions
{
    /// <summary>
    /// Uses the CL.Data connection <paramref name="connectionName"/> for every WebLogic store, or the
    /// ones in <see cref="WebDataOptions.Stores"/>. Tables (<c>wl_*</c>) are created at startup and
    /// expired rows are swept. Load <c>DataLibrary</c> and the provider library (for example
    /// <c>MySqlDataLibrary</c>) before WebLogic.
    /// </summary>
    public static WebLogicBuilder UseDataStores(this WebLogicBuilder builder, string connectionName = "Default", Action<WebDataOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionName);
        var options = new WebDataOptions { ConnectionName = connectionName };
        configure?.Invoke(options);
        if (options.SweepInterval <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(configure), "SweepInterval must be positive.");

        var services = builder.Services;
        services.AddSingleton(options);
        services.TryAddSingleton<WebDataDatabase>();

        List<Func<Result<EntityMap>>> tables = [];
        void Use<TContract, TStore>(WebDataStores flag, params Func<Result<EntityMap>>[] rows) where TContract : class where TStore : class, TContract
        {
            if (!options.Stores.HasFlag(flag))
                return;
            services.TryAddSingleton<TStore>();
            services.Replace(ServiceDescriptor.Singleton<TContract>(sp => sp.GetRequiredService<TStore>()));
            tables.AddRange(rows);
        }

        Use<IWebUserStore, DataWebUserStore>(WebDataStores.Users, WebDataTables.Map<UserRow>, WebDataTables.Map<UserLoginRow>);
        Use<IWebSessionStore, DataWebSessionStore>(WebDataStores.Sessions, WebDataTables.Map<SessionRow>);
        Use<IWebApiKeyStore, DataWebApiKeyStore>(WebDataStores.ApiKeys, WebDataTables.Map<ApiKeyRow>);
        Use<IWebRecoveryCodeStore, DataWebRecoveryCodeStore>(WebDataStores.RecoveryCodes, WebDataTables.Map<RecoveryCodeRow>);
        Use<IWebOneTimeUseStore, DataWebOneTimeUseStore>(WebDataStores.OneTimeUse, WebDataTables.Map<OneTimeUseRow>);
        Use<IWebLoginThrottle, DataWebLoginThrottle>(WebDataStores.LoginThrottle, WebDataTables.Map<LoginThrottleRow>);
        Use<IWebRateLimitStore, DataWebRateLimitStore>(WebDataStores.RateLimits, WebDataTables.Map<RateLimitRow>);
        Use<IWebAuditSink, DataWebAuditSink>(WebDataStores.Audit, WebDataTables.Map<AuditRow>);
        Use<IWebWidgetSettingsStore, DataWebWidgetSettingsStore>(WebDataStores.WidgetSettings, WebDataTables.Map<WidgetSettingsRow>);
        Use<IWebDashboardLayoutStore, DataWebDashboardLayoutStore>(WebDataStores.Dashboards, WebDataTables.Map<DashboardRow>);
        Use<IWebPasskeyStore, DataWebPasskeyStore>(WebDataStores.Passkeys, WebDataTables.Map<PasskeyRow>);

        if (options.SyncSchemaOnStart && tables.Count > 0)
            AddTables(services, new WebDataTables(connectionName, () => [.. tables.Select(t => t())]));
        services.AddHostedService<WebDataSweepService>();
        return builder;
    }

    /// <summary>
    /// Adds a group of tables to the startup schema sync. The sync is the first hosted service, so
    /// the tables exist before anything else starts (DataProtection reads the key ring at startup).
    /// </summary>
    internal static void AddTables(IServiceCollection services, WebDataTables tables)
    {
        services.AddSingleton(tables);
        if (!services.Any(d => d.ServiceType == typeof(IHostedService) && d.ImplementationType == typeof(WebDataSchemaService)))
            services.Insert(0, ServiceDescriptor.Singleton<IHostedService, WebDataSchemaService>());
    }
}
