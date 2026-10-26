using System.Collections.Concurrent;
using System.Globalization;
using CL.WebLogic.Configuration.Services;
using CL.WebLogic.Templates.Rendering;

namespace CL.WebLogic.Theming.Services;

/// <summary>
/// <c>{asset:path}</c> URLs: <c>{AssetsUrlPrefix}/{path}?v={fingerprint}</c>, where the fingerprint
/// changes whenever the file changes, so the asset can be cached as immutable. Missing assets
/// get no fingerprint. <c>{asset:name:path}</c> points at a contributed asset
/// (<see cref="WebContributedAssets"/>), unless the theme has <c>plugins/{name}/{path}</c>, which wins.
/// </summary>
public sealed class ThemeAssetUrlResolver : ITemplateAssetUrlResolver
{
    private readonly ConcurrentDictionary<string, (DateTimeOffset CheckedAt, string Url)> _cache = new(StringComparer.Ordinal);
    private readonly ThemeAssetResolver _assets;
    private readonly WebLogicConfigHolder _config;
    private readonly TimeProvider _time;
    private readonly WebContributedAssets? _contributed;

    /// <summary>Creates the resolver.</summary>
    public ThemeAssetUrlResolver(ThemeAssetResolver assets, WebLogicConfigHolder config, TimeProvider? time = null, WebContributedAssets? contributed = null)
    {
        _assets = assets ?? throw new ArgumentNullException(nameof(assets));
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _time = time ?? TimeProvider.System;
        _contributed = contributed;
        if (contributed is not null)
            contributed.Changed += _ => _cache.Clear();
    }

    /// <inheritdoc/>
    public string GetUrl(string path)
    {
        var relative = (path ?? string.Empty).Trim().TrimStart('/');
        var now = _time.GetUtcNow();
        var checkSeconds = _config.Current.Theme.TemplateChangeCheckSeconds;
        if (_cache.TryGetValue(relative, out var cached) && (checkSeconds == 0 || now - cached.CheckedAt < TimeSpan.FromSeconds(checkSeconds)))
            return cached.Url;

        var url = relative.IndexOf(':', StringComparison.Ordinal) is > 0 and var colon
            ? ContributedUrl(relative[..colon], relative[(colon + 1)..].TrimStart('/'))
            : ThemeUrl(relative);

        _cache[relative] = (now, url);
        return url;
    }

    private string ThemeUrl(string relative)
    {
        var url = _config.Current.Theme.AssetsUrlPrefix.TrimEnd('/') + "/" + Escape(relative);
        return _assets.TryResolve(relative, out var file) ? url + "?v=" + Fingerprint(file) : url;
    }

    private string ContributedUrl(string name, string path)
    {
        var overridePath = "plugins/" + name + "/" + path;
        if (_assets.TryResolve(overridePath, out _))
            return ThemeUrl(overridePath);
        var url = WebContributedAssets.UrlPrefix + Uri.EscapeDataString(name) + "/" + Escape(path);
        return _contributed is not null && _contributed.TryGet(name, path, out var asset) ? url + "?v=" + asset.Fingerprint : url;
    }

    private static string Escape(string path) => string.Join('/', path.Split('/').Select(Uri.EscapeDataString));

    private static string Fingerprint(FileInfo file)
    {
        var mixed = unchecked((ulong)file.LastWriteTimeUtc.Ticks ^ ((ulong)file.Length * 0x9E3779B97F4A7C15UL));
        return mixed.ToString("x", CultureInfo.InvariantCulture);
    }
}
