using CodeLogic.Framework.Application;
using StarterWebsite.Configuration;

namespace StarterWebsite.Application;

/// <summary>
/// The CodeLogic application: owns the app config. Web features are registered on the ASP.NET
/// host (see <see cref="StarterSetup"/>), so this class stays small.
/// <list type="number">
///   <item>Configure — registers <see cref="StarterConfig"/></item>
///   <item>Initialize — reads and validates it</item>
/// </list>
/// </summary>
public sealed class StarterApplication : IApplication
{
    /// <inheritdoc/>
    public ApplicationManifest Manifest { get; } = new()
    {
        Id = "StarterWebsite",
        Name = "WebLogic Starter Website",
        Version = "4.9.0",
        Description = "Reference website for CL.WebLogic"
    };

    /// <summary>The loaded config (after Initialize).</summary>
    public StarterConfig Config { get; private set; } = new();

    // ── Phase 1: Configure ────────────────────────────────────────────────────

    /// <inheritdoc/>
    public Task OnConfigureAsync(ApplicationContext context)
    {
        context.Configuration.Register<StarterConfig>();
        return Task.CompletedTask;
    }

    // ── Phase 2: Initialize ───────────────────────────────────────────────────

    /// <inheritdoc/>
    public Task OnInitializeAsync(ApplicationContext context)
    {
        Config = context.Configuration.Get<StarterConfig>();
        var validation = Config.Validate();
        if (!validation.IsValid)
            throw new InvalidOperationException("Starter config is invalid: " + string.Join(", ", validation.Errors));
        context.Logger.Info($"{Manifest.Name} initialized (registration {(Config.AllowRegistration ? "open" : "closed")})");
        return Task.CompletedTask;
    }

    // ── Phase 3: Start / Phase 4: Stop ────────────────────────────────────────

    /// <inheritdoc/>
    public Task OnStartAsync(ApplicationContext context) => Task.CompletedTask;

    /// <inheritdoc/>
    public Task OnStopAsync() => Task.CompletedTask;
}
