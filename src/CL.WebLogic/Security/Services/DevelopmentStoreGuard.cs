using CL.WebLogic.Configuration.Services;
using CL.WebLogic.Hosting.Services;
using CL.WebLogic.Security.Sessions;
using Microsoft.Extensions.Hosting;

namespace CL.WebLogic.Security.Services;

/// <summary>
/// Refuses to start outside Development while a security contract still resolves to an
/// in-memory development store (unless <c>Security.AllowDevelopmentStores</c> is set).
/// </summary>
public sealed class DevelopmentStoreGuard : IHostedService
{
    /// <summary>Security contracts checked at startup.</summary>
    public static IReadOnlyList<Type> GuardedContracts { get; } =
    [
        typeof(IWebSessionStore), typeof(Auth.Users.IWebUserStore), typeof(Auth.ApiKeys.IWebApiKeyStore),
        typeof(Auth.TwoFactor.IWebRecoveryCodeStore), typeof(Auth.Notifications.IWebSecurityNotifier)
    ];

    /// <summary>App data contracts: an in-memory default only loses data, so it is a warning outside Development, not an error.</summary>
    public static IReadOnlyList<Type> DataContracts { get; } =
    [
        typeof(Widgets.Services.IWebWidgetSettingsStore), typeof(Dashboard.Services.IWebDashboardLayoutStore)
    ];

    private readonly IServiceProvider _services;
    private readonly IHostEnvironment _environment;
    private readonly WebLogicConfigHolder _config;
    private readonly IWebLogicContext _context;

    /// <summary>Creates the guard.</summary>
    public DevelopmentStoreGuard(IServiceProvider services, IHostEnvironment environment, WebLogicConfigHolder config, IWebLogicContext context)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
        _environment = environment ?? throw new ArgumentNullException(nameof(environment));
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <inheritdoc/>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_environment.IsDevelopment())
        {
            var data = DataContracts.Select(type => (Contract: type, Instance: _services.GetService(type)))
                .Where(x => x.Instance is IWebDevelopmentStore)
                .Select(x => x.Contract.Name)
                .ToList();
            if (data.Count > 0)
                _context.Logger.Warning($"In-memory stores are used for {string.Join(", ", data)}: saved data is lost on restart. Register app stores (web.UseWidgetSettingsStore<T>(), web.UseDashboardStore<T>()).");

            // Sessions' CSRF secrets, account tokens and every 2FA secret are DataProtection-protected.
            var keys = _services.GetService(typeof(Microsoft.Extensions.Options.IOptions<Microsoft.AspNetCore.DataProtection.KeyManagement.KeyManagementOptions>))
                as Microsoft.Extensions.Options.IOptions<Microsoft.AspNetCore.DataProtection.KeyManagement.KeyManagementOptions>;
            if (keys?.Value.XmlRepository is null)
                _context.Logger.Warning("DataProtection keys use the default location: in a container or on a new machine they are lost on restart, and with them every two-factor secret, reset link and pending token. Persist them (services.AddDataProtection().PersistKeysTo…()).");
        }

        var offenders = GuardedContracts
            .Select(type => (Contract: type, Instance: _services.GetService(type)))
            .Where(x => x.Instance is IWebDevelopmentStore)
            .Select(x => $"{x.Contract.Name} → {x.Instance!.GetType().Name}")
            .ToList();
        if (offenders.Count == 0)
            return Task.CompletedTask;

        var message = "In-memory development stores are registered: " + string.Join(", ", offenders) + ".";
        if (_environment.IsDevelopment())
        {
            _context.Logger.Info(message + " Register real stores before deploying.");
            return Task.CompletedTask;
        }
        if (_config.Current.Security.AllowDevelopmentStores)
        {
            _context.Logger.Warning(message + " Allowed by Security.AllowDevelopmentStores.");
            return Task.CompletedTask;
        }
        throw new InvalidOperationException(message + " Register real implementations (e.g. web.UseSessionStore<T>(), web.UseUserStore<T>(), web.UseMailNotifications()) or set Security.AllowDevelopmentStores.");
    }

    /// <inheritdoc/>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
