using System.Globalization;
using CL.WebLogic.Configuration.Services;
using CL.WebLogic.Results;
using CL.WebLogic.Routing;
using CL.WebLogic.Routing.Models;
using CL.WebLogic.Routing.Services;

namespace CL.WebLogic.Theming.Services;

/// <summary>
/// System contributor that serves contributed assets (<see cref="WebContributedAssets"/>) at
/// <c>/_weblogic/plugins/{name}/{**path}</c> with ETags and immutable caching for fingerprinted URLs.
/// </summary>
public sealed class PluginAssetContributor : IWebContributor
{
    private readonly WebContributedAssets _assets;
    private readonly WebLogicConfigHolder _config;

    /// <summary>Creates the contributor.</summary>
    public PluginAssetContributor(WebContributedAssets assets, WebLogicConfigHolder config)
    {
        _assets = assets ?? throw new ArgumentNullException(nameof(assets));
        _config = config ?? throw new ArgumentNullException(nameof(config));
    }

    /// <summary>Descriptor for this contributor.</summary>
    public static WebContributorDescriptor Descriptor { get; } = new()
    {
        Id = "weblogic.plugin-assets",
        Name = "Plugin assets",
        Kind = "System",
        Description = "Serves static files embedded in contributors and plugins."
    };

    /// <inheritdoc/>
    public Task ContributeAsync(WebContributionBuilder builder, CancellationToken cancellationToken)
    {
        builder.Map(WebRouteKind.Asset, WebContributedAssets.UrlPrefix + "{name}/{**path}", ServeAsync, "GET")
            .AllowAnonymous()
            .WithName("weblogic.plugin-asset")
            .WithDescription("Static file embedded in a contributor or plugin");
        return Task.CompletedTask;
    }

    private ValueTask<WebResult> ServeAsync(Runtime.Models.WebRequestContext request, CancellationToken cancellationToken)
    {
        var name = request.GetRouteValue("name");
        var path = request.GetRouteValue("path");
        var extension = Path.GetExtension(path ?? "");
        if (name is null || path is null || !ThemeAssetResolver.IsSafeRelativePath(path)
            || !_config.Current.Theme.AllowedAssetExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase)
            || !_assets.TryGet(name, path, out var asset))
            return ValueTask.FromResult<WebResult>(WebResult.Status(404));

        var cacheControl = string.IsNullOrEmpty(request.GetQuery("v"))
            ? "public, max-age=" + _config.Current.Theme.AssetCacheSeconds.ToString(CultureInfo.InvariantCulture)
            : "public, max-age=31536000, immutable";
        var result = new FileResult(new MemoryStream(asset.Content, writable: false), asset.ContentType, downloadName: null, lastModified: asset.LastModified)
        {
            ETag = "\"" + asset.Fingerprint + "\""
        };
        result.WithHeader("Cache-Control", cacheControl);
        return ValueTask.FromResult<WebResult>(result);
    }
}
