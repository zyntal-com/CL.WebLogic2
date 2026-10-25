using System.Collections.Concurrent;
using System.Globalization;
using CL.WebLogic.Configuration.Services;
using CL.WebLogic.Templates.Rendering;

namespace CL.WebLogic.Theming.Services;

/// <summary>
/// <c>{asset:path}</c> URLs: <c>{AssetsUrlPrefix}/{path}?v={fingerprint}</c>, where the fingerprint
/// changes whenever the file changes, so the asset can be cached as immutable. Missing assets
/// get no fingerprint.
/// </summary>
public sealed class ThemeAssetUrlResolver : ITemplateAssetUrlResolver
{
    private readonly ConcurrentDictionary<string, (DateTimeOffset CheckedAt, string Url)> _cache = new(StringComparer.Ordinal);
    private readonly ThemeAssetResolver _assets;
    private readonly WebLogicConfigHolder _config;
    private readonly TimeProvider _time;

    /// <summary>Creates the resolver.</summary>
    public ThemeAssetUrlResolver(ThemeAssetResolver assets, WebLogicConfigHolder config, TimeProvider? time = null)
    {
        _assets = assets ?? throw new ArgumentNullException(nameof(assets));
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _time = time ?? TimeProvider.System;
    }

    /// <inheritdoc/>
    public string GetUrl(string path)
    {
        var relative = (path ?? string.Empty).Trim().TrimStart('/');
        var now = _time.GetUtcNow();
        var checkSeconds = _config.Current.Theme.TemplateChangeCheckSeconds;
        if (_cache.TryGetValue(relative, out var cached) && (checkSeconds == 0 || now - cached.CheckedAt < TimeSpan.FromSeconds(checkSeconds)))
            return cached.Url;

        var prefix = _config.Current.Theme.AssetsUrlPrefix.TrimEnd('/');
        var url = prefix + "/" + string.Join('/', relative.Split('/').Select(Uri.EscapeDataString));
        if (_assets.TryResolve(relative, out var file))
            url += "?v=" + Fingerprint(file);

        _cache[relative] = (now, url);
        return url;
    }

    private static string Fingerprint(FileInfo file)
    {
        var mixed = unchecked((ulong)file.LastWriteTimeUtc.Ticks ^ ((ulong)file.Length * 0x9E3779B97F4A7C15UL));
        return mixed.ToString("x", CultureInfo.InvariantCulture);
    }
}
