using System.Collections.Concurrent;
using CL.WebLogic.Configuration.Services;

namespace CL.WebLogic.Theming.Services;

/// <summary>
/// Maps a request path to a file inside the theme's assets folder, refusing anything that
/// could escape it: traversal segments, rooted or drive paths, backslashes, alternate data
/// streams, dotfiles, symbolic links and extensions outside the allowlist. Results (including
/// misses) are reused for <see cref="RecheckInterval"/>, and small files are kept in memory, so a
/// busy page does not stat and open every asset on every request.
/// </summary>
public sealed class ThemeAssetResolver
{
    /// <summary>How long a resolution (and cached content) is trusted before the file is checked again.</summary>
    public static readonly TimeSpan RecheckInterval = TimeSpan.FromSeconds(2);

    /// <summary>Files up to this size are served from memory.</summary>
    public const int MaxCachedFileBytes = 256 * 1024;

    private const int MaxEntries = 2048;
    private static readonly StringComparison PathComparison =
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    private readonly ConcurrentDictionary<string, Resolution> _resolutions = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, Content> _contents = new(StringComparer.Ordinal);
    private readonly IWebThemeRoot _root;
    private readonly WebLogicConfigHolder _config;
    private readonly TimeProvider _time;

    /// <summary>Creates the resolver.</summary>
    public ThemeAssetResolver(IWebThemeRoot root, WebLogicConfigHolder config, TimeProvider? time = null)
    {
        _root = root ?? throw new ArgumentNullException(nameof(root));
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _time = time ?? TimeProvider.System;
    }

    /// <summary>Resolves <paramref name="relativePath"/> (the part after the assets URL prefix) to an existing file.</summary>
    public bool TryResolve(string? relativePath, out FileInfo file)
    {
        file = null!;
        if (!IsSafeRelativePath(relativePath))
            return false;

        var key = _root.RootPath + "|" + relativePath;
        var now = _time.GetTimestamp();
        if (_resolutions.TryGetValue(key, out var cached) && _time.GetElapsedTime(cached.CheckedAt, now) < RecheckInterval)
        {
            file = cached.File!;
            return cached.File is not null;
        }

        var resolved = Resolve(relativePath!);
        if (_resolutions.Count >= MaxEntries)
            _resolutions.Clear();
        _resolutions[key] = new Resolution(resolved, now);
        file = resolved!;
        return resolved is not null;
    }

    /// <summary>
    /// The bytes of <paramref name="file"/> when it is small enough to keep in memory (re-read when
    /// its timestamp or length changes); null for larger files, which are streamed.
    /// </summary>
    public byte[]? GetSmallFileContent(FileInfo file)
    {
        ArgumentNullException.ThrowIfNull(file);
        if (file.Length > MaxCachedFileBytes)
            return null;
        if (_contents.TryGetValue(file.FullName, out var cached) && cached.LastWriteUtc == file.LastWriteTimeUtc && cached.Bytes.Length == file.Length)
            return cached.Bytes;

        byte[] bytes;
        try
        {
            bytes = File.ReadAllBytes(file.FullName);
        }
        catch (IOException)
        {
            return null;
        }
        if (_contents.Count >= MaxEntries)
            _contents.Clear();
        _contents[file.FullName] = new Content(bytes, file.LastWriteTimeUtc);
        return bytes;
    }

    private FileInfo? Resolve(string relativePath)
    {
        var theme = _config.Current.Theme;
        var assetsRoot = Path.GetFullPath(Path.Combine(_root.RootPath, theme.AssetsFolder));
        var assetsRootWithSeparator = assetsRoot.EndsWith(Path.DirectorySeparatorChar) ? assetsRoot : assetsRoot + Path.DirectorySeparatorChar;

        var fullPath = Path.GetFullPath(Path.Combine(assetsRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        if (!fullPath.StartsWith(assetsRootWithSeparator, PathComparison))
            return null;

        var extension = Path.GetExtension(fullPath);
        if (string.IsNullOrEmpty(extension) || !theme.AllowedAssetExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
            return null;

        var candidate = new FileInfo(fullPath);
        if (!candidate.Exists || ThemeLinks.AnyBelow(_root.RootPath, candidate))
            return null;
        return candidate;
    }

    /// <summary>Syntactic checks done before touching the file system.</summary>
    internal static bool IsSafeRelativePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || path.Length > 512)
            return false;

        if (path.Contains('\\') || path.Contains(':') || path.StartsWith('/'))
            return false;

        foreach (var c in path)
        {
            if (char.IsControl(c))
                return false;
        }

        foreach (var segment in path.Split('/'))
        {
            // Empty (//), current/parent (., ..) and dot-prefixed (.git, .env) segments are refused.
            if (segment.Length == 0 || segment[0] == '.' || segment.EndsWith('.') || segment.EndsWith(' '))
                return false;
        }

        return true;
    }

    private sealed record Resolution(FileInfo? File, long CheckedAt);

    private sealed record Content(byte[] Bytes, DateTime LastWriteUtc);
}
