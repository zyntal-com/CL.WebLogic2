using CL.WebLogic.Configuration.Models;
using CL.WebLogic.Configuration.Services;
using CL.WebLogic.Hosting.Services;
using CodeLogic.Core.Configuration;
using CodeLogic.Framework.Libraries;

namespace CL.WebLogic;

/// <summary>
/// <b>CL.WebLogic</b> — CodeLogic library providing the web toolkit: routing, templates,
/// forms, the security pipeline, auth flows, widgets and realtime on top of ASP.NET Core.
/// <para>
/// Lifecycle:
/// <list type="number">
///   <item><description><b>Configure</b> — registers <see cref="WebLogicConfig"/> (<c>config.weblogic.json</c>).</description></item>
///   <item><description><b>Initialize</b> — validates config, creates the config holder and the <see cref="IWebLogicContext"/>.</description></item>
///   <item><description><b>Start</b> — logs that the toolkit is ready for the ASP.NET host.</description></item>
///   <item><description><b>Stop</b> — clears service references.</description></item>
/// </list>
/// Config changes made while running (admin UI, reload, file watcher) are applied live —
/// see <see cref="OnConfigChangedAsync"/>. Fields marked <c>RequiresRestart</c> are stored
/// and reported, but only take full effect after a restart.
/// </para>
/// <para>
/// The ASP.NET side is wired with <c>builder.AddCodeLogicWebLogic()</c>,
/// <c>app.UseCodeLogicWebLogic()</c> and <c>app.MapCodeLogicWebLogic()</c>.
/// </para>
/// </summary>
public sealed class WebLogicLibrary : ILibrary, IConfigReloadable
{
    /// <inheritdoc/>
    public LibraryManifest Manifest { get; } = new()
    {
        Id = "CL.WebLogic",
        Name = "WebLogic Library",
        Version = CL.Internal.InternalLibraryVersion.Current,
        Description = "Web application toolkit: routing, templates, forms, security pipeline, auth flows and realtime",
        Author = "Zyntal.com",
        Tags = ["web", "aspnetcore", "templates", "forms", "security", "realtime"]
    };

    private LibraryContext? _context;
    private WebLogicConfigHolder? _config;
    private ConfigApplyCoordinator? _configApplier;

    /// <summary>
    /// The toolkit context shared with the ASP.NET host. Null before <see cref="OnInitializeAsync"/>
    /// and after <see cref="OnStopAsync"/>.
    /// </summary>
    public IWebLogicContext? Context { get; private set; }

    // ── Phase 1: Configure ────────────────────────────────────────────────────

    /// <inheritdoc/>
    public Task OnConfigureAsync(LibraryContext context)
    {
        _context = context;
        context.Logger.Info($"Configuring {Manifest.Name} v{Manifest.Version}");

        context.Configuration.Register<WebLogicConfig>("weblogic");

        return Task.CompletedTask;
    }

    // ── Phase 2: Initialize ───────────────────────────────────────────────────

    /// <inheritdoc/>
    public Task OnInitializeAsync(LibraryContext context)
    {
        _context = context;
        context.Logger.Info($"Initializing {Manifest.Name}");

        var config = context.Configuration.Get<WebLogicConfig>();

        var validation = config.Validate();
        if (!validation.IsValid)
        {
            var errors = string.Join(", ", validation.Errors);
            context.Logger.Error($"{Manifest.Name} configuration is invalid: {errors}");
            throw new InvalidOperationException($"{Manifest.Name} configuration is invalid: {errors}");
        }

        _config = new WebLogicConfigHolder(config);
        _configApplier = new ConfigApplyCoordinator(_config, context.Logger, context.Events);
        Context = new LibraryWebLogicContext(context, _config, HealthCheckAsync);

        context.Logger.Info($"{Manifest.Name} initialized (site={config.SiteName})");
        return Task.CompletedTask;
    }

    // ── Phase 3: Start ────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public Task OnStartAsync(LibraryContext context)
    {
        context.Logger.Info($"{Manifest.Name} started — ready for the ASP.NET host");
        return Task.CompletedTask;
    }

    // ── Phase 4: Stop ─────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public Task OnStopAsync()
    {
        _context?.Logger.Info($"Stopping {Manifest.Name}");
        Context = null;
        _configApplier = null;
        _config = null;
        return Task.CompletedTask;
    }

    // ── Config reload ─────────────────────────────────────────────────────────

    /// <summary>
    /// Applies a live change to the <c>weblogic</c> section. Services read the swapped config
    /// on their next use; restart-only fields are flagged instead of failing the change.
    /// </summary>
    public async Task OnConfigChangedAsync(LibraryContext context, ConfigChangedEventArgs change, CancellationToken cancellationToken)
    {
        if (change.NewValue is not WebLogicConfig next || _configApplier is null) return;
        await _configApplier.ApplyAsync(next, cancellationToken).ConfigureAwait(false);
    }

    // ── Health check ──────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public Task<HealthStatus> HealthCheckAsync()
    {
        if (_config is null)
            return Task.FromResult(HealthStatus.Unhealthy($"{Manifest.Name} is not initialized"));

        if (_configApplier?.RestartPending == true)
            return Task.FromResult(HealthStatus.Degraded($"{Manifest.Name} has config changes that require a restart"));

        return Task.FromResult(HealthStatus.Healthy($"{Manifest.Name} is running"));
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        Context = null;
        _configApplier = null;
        _config = null;
    }
}
