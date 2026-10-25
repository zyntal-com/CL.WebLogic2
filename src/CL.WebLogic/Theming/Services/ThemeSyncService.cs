using CL.WebLogic.Configuration.Services;
using CL.WebLogic.Hosting.Services;
using CL.WebLogic.Theming.Events;
using Microsoft.Extensions.Hosting;

namespace CL.WebLogic.Theming.Services;

/// <summary>
/// Synchronizes the theme from its source at startup (before requests are served) and on demand
/// via <see cref="RefreshAsync"/> — e.g. from an admin action or a Git push webhook the app maps.
/// After a sync the template and asset caches revalidate against the new files.
/// </summary>
public sealed class ThemeSyncService : IHostedService, IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly IEnumerable<IWebThemeSource> _sources;
    private readonly IWebThemeRoot _root;
    private readonly WebLogicConfigHolder _config;
    private readonly ThemeTemplateLoader _templates;
    private readonly IWebLogicContext _context;

    /// <summary>Creates the service.</summary>
    public ThemeSyncService(IEnumerable<IWebThemeSource> sources, IWebThemeRoot root, WebLogicConfigHolder config, ThemeTemplateLoader templates, IWebLogicContext context)
    {
        _sources = sources ?? throw new ArgumentNullException(nameof(sources));
        _root = root ?? throw new ArgumentNullException(nameof(root));
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _templates = templates ?? throw new ArgumentNullException(nameof(templates));
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <summary>Time of the last successful sync (UTC), or null.</summary>
    public DateTimeOffset? LastSyncedAt { get; private set; }

    /// <inheritdoc/>
    public Task StartAsync(CancellationToken cancellationToken) => RefreshAsync(cancellationToken);

    /// <inheritdoc/>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc/>
    public void Dispose() => _gate.Dispose();

    /// <summary>Synchronizes the theme now and returns the local theme root.</summary>
    public async Task<string> RefreshAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var theme = _config.Current.Theme;
            var source = _sources.LastOrDefault(s => s.Kind == theme.Source)
                ?? throw new InvalidOperationException($"No theme source is registered for '{theme.Source}'.");

            var path = await source.SyncAsync(theme, cancellationToken).ConfigureAwait(false);
            if (_root is WebThemeRoot webRoot)
                webRoot.SetSyncedPath(path);

            _templates.Invalidate();
            LastSyncedAt = DateTimeOffset.UtcNow;
            if (theme.Source != Configuration.Models.ThemeSourceKind.Local)
                _context.Logger.Info($"Theme synchronized from {theme.Source} into {path}");
            await _context.Events.PublishAsync(new ThemeSyncedEvent(theme.Source.ToString(), path)).ConfigureAwait(false);
            return path;
        }
        finally
        {
            _gate.Release();
        }
    }
}
