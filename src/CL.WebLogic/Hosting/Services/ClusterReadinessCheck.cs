using CL.WebLogic.Auth.ApiKeys;
using CL.WebLogic.Auth.Notifications;
using CL.WebLogic.Auth.Throttling;
using CL.WebLogic.Auth.Tokens;
using CL.WebLogic.Auth.Passkeys;
using CL.WebLogic.Auth.TwoFactor;
using CL.WebLogic.Auth.Users;
using CL.WebLogic.Caching.Services;
using CL.WebLogic.Configuration.Services;
using CL.WebLogic.Dashboard.Services;
using CL.WebLogic.Hosting.Models;
using CL.WebLogic.Realtime.Services;
using CL.WebLogic.Security.RateLimiting;
using CL.WebLogic.Security.Services;
using CL.WebLogic.Security.Sessions;
using CL.WebLogic.Widgets.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace CL.WebLogic.Hosting.Services;

/// <summary>
/// Inspects the registered contracts for state that only lives in this process. Used by
/// <see cref="ClusterGuard"/> at startup; apps can call it from their own diagnostics.
/// </summary>
public sealed class ClusterReadinessCheck
{
    /// <summary>Contracts whose state must be shared: per-node copies break sign-in, replay protection or limits.</summary>
    public static IReadOnlyList<(Type Contract, string Why)> SharedContracts { get; } =
    [
        (typeof(IWebSessionStore), "a session created on one node is unknown on the others"),
        (typeof(IWebUserStore), "accounts would differ per node"),
        (typeof(IWebApiKeyStore), "API keys would differ per node"),
        (typeof(IWebRecoveryCodeStore), "a used recovery code would still work on other nodes"),
        (typeof(IWebPasskeyStore), "passkeys registered on one node would be unknown on the others"),
        (typeof(IWebOneTimeUseStore), "TOTP codes and single-use links could be replayed on another node"),
        (typeof(IWebLoginThrottle), "each node would allow the full number of login attempts"),
        (typeof(IWebRateLimitStore), "each node would apply its own limits (N nodes = N× the limit)"),
        (typeof(IWebRealtimeBackplane), "realtime messages would only reach clients on the publishing node"),
        (typeof(IWebSecurityNotifier), "security mail would be written to a local outbox")
    ];

    /// <summary>Contracts where per-node state is inconsistent but not unsafe.</summary>
    public static IReadOnlyList<(Type Contract, string Why)> AdvisedContracts { get; } =
    [
        (typeof(IWebWidgetSettingsStore), "widget settings would differ per node"),
        (typeof(IWebDashboardLayoutStore), "dashboards would differ per node"),
        (typeof(IWebFragmentCache), "evicting a tag only clears this node's fragments")
    ];

    private readonly IServiceProvider _services;
    private readonly WebLogicConfigHolder _config;
    private readonly IHostEnvironment _environment;

    /// <summary>Creates the check.</summary>
    public ClusterReadinessCheck(IServiceProvider services, WebLogicConfigHolder config, IHostEnvironment environment)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _environment = environment ?? throw new ArgumentNullException(nameof(environment));
    }

    /// <summary>Evaluates the current registrations.</summary>
    public ClusterReadinessReport Evaluate()
    {
        var allowed = new HashSet<string>(_config.Current.Cluster.AllowNodeLocal, StringComparer.OrdinalIgnoreCase);
        var errors = new List<string>();
        var warnings = new List<string>();

        foreach (var (contract, why) in SharedContracts)
        {
            if (NodeLocalImplementation(contract) is not { } local)
                continue;
            var message = $"{contract.Name} → {local}: {why}.";
            if (allowed.Contains(contract.Name)) warnings.Add(message + " (allowed by Cluster.AllowNodeLocal)");
            else errors.Add(message);
        }
        foreach (var (contract, why) in AdvisedContracts)
        {
            if (NodeLocalImplementation(contract) is { } local)
                warnings.Add($"{contract.Name} → {local}: {why}.");
        }

        if (_services.GetService(typeof(IOutputCacheStore)) is { } outputStore && outputStore.GetType().Name.Contains("Memory", StringComparison.Ordinal))
            warnings.Add("IOutputCacheStore → in-memory: evicting a tag only clears this node's cached pages (register a shared IOutputCacheStore).");

        var keyManagement = (_services.GetService(typeof(IOptions<KeyManagementOptions>)) as IOptions<KeyManagementOptions>)?.Value;
        if (keyManagement?.XmlRepository is Microsoft.AspNetCore.DataProtection.Repositories.FileSystemXmlRepository fileRepository)
            warnings.Add($"DataProtection keys are in the folder '{fileRepository.Directory.FullName}': every node must mount the same folder (or use PersistKeysToCodeLogicStorage).");
        else if (keyManagement?.XmlRepository is null)
            errors.Add("DataProtection keys are not persisted to a shared repository: cookies, CSRF tokens, reset links and 2FA secrets made on one node cannot be read on another (call services.AddDataProtection().PersistKeysToCodeLogicStorage(connectionId) or another PersistKeysTo…() with storage every node shares).");

        var discriminator = (_services.GetService(typeof(IOptions<DataProtectionOptions>)) as IOptions<DataProtectionOptions>)?.Value.ApplicationDiscriminator;
        if (string.IsNullOrEmpty(discriminator)
            || string.Equals(discriminator.TrimEnd('/', '\\'), _environment.ContentRootPath.TrimEnd('/', '\\'), StringComparison.OrdinalIgnoreCase))
            warnings.Add("DataProtection has no explicit application name: nodes deployed to different paths cannot share keys (call SetApplicationName(…)).");

        return new ClusterReadinessReport(errors, warnings);
    }

    private string? NodeLocalImplementation(Type contract)
    {
        var instance = _services.GetService(contract);
        return instance is IWebNodeLocalStore or IWebDevelopmentStore ? instance.GetType().Name : null;
    }
}
