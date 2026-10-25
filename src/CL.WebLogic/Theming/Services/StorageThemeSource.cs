using CL.Storage;
using CL.WebLogic.Configuration.Models;
using CL.WebLogic.Hosting.Services;

namespace CL.WebLogic.Theming.Services;

/// <summary>
/// <c>Source = Storage</c>: downloads the theme folder from a CL.Storage connection (S3, Azure,
/// FTP, local disk…) into <c>{data}/themes/storage-{connection}</c>. Files are replaced atomically,
/// so a failed sync never leaves a half-written theme.
/// </summary>
public sealed class StorageThemeSource : IWebThemeSource
{
    private readonly IWebLibraryResolver _libraries;
    private readonly IWebLogicContext _context;

    /// <summary>Creates the source.</summary>
    public StorageThemeSource(IWebLibraryResolver libraries, IWebLogicContext context)
    {
        _libraries = libraries ?? throw new ArgumentNullException(nameof(libraries));
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <inheritdoc/>
    public ThemeSourceKind Kind => ThemeSourceKind.Storage;

    /// <inheritdoc/>
    public async Task<string> SyncAsync(ThemeConfig config, CancellationToken cancellationToken)
    {
        var storage = _libraries.GetLibrary<StorageLibrary>()
            ?? throw new InvalidOperationException("Theme source is Storage, but CL.Storage is not loaded. Load StorageLibrary before WebLogic.");

        var safeName = new string(config.StorageConnectionId.Select(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' ? c : '_').ToArray());
        var localPath = Path.Combine(_context.DataDirectory, "themes", "storage-" + safeName);

        var result = await storage.DownloadDirectoryAsync(config.StorageConnectionId, config.StoragePath, localPath, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (result.IsFailure)
        {
            if (Directory.Exists(localPath))
            {
                _context.Logger.Warning($"Theme download from storage '{config.StorageConnectionId}' failed ({result.Error?.Message}); serving the last synchronized copy.");
                return localPath;
            }
            throw new InvalidOperationException($"Theme download from storage '{config.StorageConnectionId}' failed: {result.Error?.Message}");
        }

        return localPath;
    }
}
