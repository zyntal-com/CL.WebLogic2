using CL.WebLogic.Configuration.Models;
using CL.WebLogic.Configuration.Services;
using Microsoft.Extensions.Hosting;

namespace CL.WebLogic.Theming.Services;

/// <summary>
/// The active theme root. Local themes resolve straight from config; remote themes resolve to
/// the folder <see cref="ThemeSyncService"/> synchronized into (until then, the configured local path).
/// </summary>
public sealed class WebThemeRoot : IWebThemeRoot
{
    private readonly WebLogicConfigHolder _config;
    private readonly IHostEnvironment _environment;
    private volatile string? _syncedPath;

    /// <summary>Creates the root.</summary>
    public WebThemeRoot(WebLogicConfigHolder config, IHostEnvironment environment)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _environment = environment ?? throw new ArgumentNullException(nameof(environment));
    }

    /// <inheritdoc/>
    public string RootPath
    {
        get
        {
            var theme = _config.Current.Theme;
            return theme.Source != ThemeSourceKind.Local && _syncedPath is { } synced
                ? synced
                : LocalThemeSource.Resolve(theme, _environment.ContentRootPath);
        }
    }

    /// <summary>Sets the folder a remote theme was synchronized into.</summary>
    internal void SetSyncedPath(string path) => _syncedPath = Path.GetFullPath(path);
}
