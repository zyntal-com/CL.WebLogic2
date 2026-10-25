using CL.WebLogic.Configuration.Models;

namespace CL.WebLogic.Theming.Services;

/// <summary>
/// Brings a theme onto the local disk. Remote sources (Git, Storage) synchronize into the
/// WebLogic data directory; templates and assets are always served from the local copy.
/// </summary>
public interface IWebThemeSource
{
    /// <summary>The source kind this implementation handles.</summary>
    ThemeSourceKind Kind { get; }

    /// <summary>Synchronizes the theme and returns the absolute local theme root.</summary>
    Task<string> SyncAsync(ThemeConfig config, CancellationToken cancellationToken);
}
