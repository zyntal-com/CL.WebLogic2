using CL.WebLogic.Configuration.Models;
using Microsoft.Extensions.Hosting;

namespace CL.WebLogic.Theming.Services;

/// <summary><c>Source = Local</c>: the configured folder (relative to the content root).</summary>
public sealed class LocalThemeSource : IWebThemeSource
{
    private readonly IHostEnvironment _environment;

    /// <summary>Creates the source.</summary>
    public LocalThemeSource(IHostEnvironment environment) =>
        _environment = environment ?? throw new ArgumentNullException(nameof(environment));

    /// <inheritdoc/>
    public ThemeSourceKind Kind => ThemeSourceKind.Local;

    /// <inheritdoc/>
    public Task<string> SyncAsync(ThemeConfig config, CancellationToken cancellationToken) =>
        Task.FromResult(Resolve(config, _environment.ContentRootPath));

    /// <summary>Absolute path of a local theme folder.</summary>
    public static string Resolve(ThemeConfig config, string contentRoot)
    {
        var path = Path.IsPathRooted(config.LocalPath) ? config.LocalPath : Path.Combine(contentRoot, config.LocalPath);
        return Path.GetFullPath(path);
    }
}
