using CodeLogic.Framework.Application;
using MiniBlog.Configuration;

namespace MiniBlog.Application;

/// <summary>
/// The CodeLogic application: owns the blog config. Web features are registered on the ASP.NET
/// host (see <see cref="MiniBlogSetup"/>).
/// </summary>
public sealed class MiniBlogApplication : IApplication
{
    /// <inheritdoc/>
    public ApplicationManifest Manifest { get; } = new()
    {
        Id = "MiniBlog",
        Name = "MiniBlog",
        Version = "4.9.0",
        Description = "Blog sample for CL.WebLogic on CL.MySQL2"
    };

    /// <summary>The loaded config (after Initialize).</summary>
    public MiniBlogConfig Config { get; private set; } = new();

    // ── Phase 1: Configure ────────────────────────────────────────────────────

    /// <inheritdoc/>
    public Task OnConfigureAsync(ApplicationContext context)
    {
        context.Configuration.Register<MiniBlogConfig>();
        return Task.CompletedTask;
    }

    // ── Phase 2: Initialize ───────────────────────────────────────────────────

    /// <inheritdoc/>
    public Task OnInitializeAsync(ApplicationContext context)
    {
        Config = context.Configuration.Get<MiniBlogConfig>();
        var validation = Config.Validate();
        if (!validation.IsValid)
            throw new InvalidOperationException("MiniBlog config is invalid: " + string.Join(", ", validation.Errors));
        context.Logger.Info($"{Manifest.Name} initialized ({Config.PageSize} posts per page)");
        return Task.CompletedTask;
    }

    // ── Phase 3: Start / Phase 4: Stop ────────────────────────────────────────

    /// <inheritdoc/>
    public Task OnStartAsync(ApplicationContext context) => Task.CompletedTask;

    /// <inheritdoc/>
    public Task OnStopAsync() => Task.CompletedTask;
}
