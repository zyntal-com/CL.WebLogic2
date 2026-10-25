using System.Globalization;
using CL.WebLogic.Configuration.Services;
using CL.WebLogic.Results;
using CL.WebLogic.Routing;
using CL.WebLogic.Routing.Models;
using CL.WebLogic.Routing.Services;
using Microsoft.AspNetCore.StaticFiles;

namespace CL.WebLogic.Theming.Services;

/// <summary>
/// System contributor that serves the theme's assets folder at <c>Theme.AssetsUrlPrefix</c>.
/// Files are streamed with range support, ETag / Last-Modified validation, and a long
/// immutable cache lifetime when the URL carries a <c>?v=</c> fingerprint.
/// </summary>
public sealed class ThemeAssetContributor : IWebContributor
{
    /// <summary>Contributor id used for the asset routes.</summary>
    public const string ContributorId = "weblogic.theme-assets";

    private static readonly FileExtensionContentTypeProvider ContentTypes = new();

    private readonly ThemeAssetResolver _resolver;
    private readonly WebLogicConfigHolder _config;

    /// <summary>Creates the contributor.</summary>
    public ThemeAssetContributor(ThemeAssetResolver resolver, WebLogicConfigHolder config)
    {
        _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
        _config = config ?? throw new ArgumentNullException(nameof(config));
    }

    /// <summary>Descriptor for this contributor.</summary>
    public static WebContributorDescriptor Descriptor { get; } = new()
    {
        Id = ContributorId,
        Name = "Theme assets",
        Kind = "System",
        Description = "Serves the theme's public assets folder."
    };

    /// <inheritdoc/>
    public Task ContributeAsync(WebContributionBuilder builder, CancellationToken cancellationToken)
    {
        var prefix = _config.Current.Theme.AssetsUrlPrefix;
        builder.Map(WebRouteKind.Asset, WebRoutePattern.Combine(prefix, "{**path}"), ServeAsync, "GET")
            .AllowAnonymous()
            .WithName("weblogic.theme-asset")
            .WithDescription("Theme static asset");
        return Task.CompletedTask;
    }

    private ValueTask<WebResult> ServeAsync(Runtime.Models.WebRequestContext request, CancellationToken cancellationToken)
    {
        if (!_resolver.TryResolve(request.GetRouteValue("path"), out var file))
            return ValueTask.FromResult<WebResult>(WebResult.Status(404));

        if (!ContentTypes.TryGetContentType(file.Name, out var contentType))
            contentType = "application/octet-stream";

        var fingerprinted = !string.IsNullOrEmpty(request.GetQuery("v"));
        var cacheControl = fingerprinted
            ? "public, max-age=31536000, immutable"
            : "public, max-age=" + _config.Current.Theme.AssetCacheSeconds.ToString(CultureInfo.InvariantCulture);

        Stream stream = _resolver.GetSmallFileContent(file) is { } bytes
            ? new MemoryStream(bytes, writable: false)
            : new FileStream(file.FullName, FileMode.Open, FileAccess.Read, FileShare.Read, 16 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);

        var result = new FileResult(stream, contentType, downloadName: null, lastModified: file.LastWriteTimeUtc)
        {
            ETag = "\"" + file.LastWriteTimeUtc.Ticks.ToString("x", CultureInfo.InvariantCulture)
                + "-" + file.Length.ToString("x", CultureInfo.InvariantCulture) + "\""
        };
        result.WithHeader("Cache-Control", cacheControl);
        return ValueTask.FromResult<WebResult>(result);
    }
}
