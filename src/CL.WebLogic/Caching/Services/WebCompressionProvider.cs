using CL.WebLogic.Configuration.Models;
using CL.WebLogic.Configuration.Services;
using CL.WebLogic.Routing.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.Extensions.Options;

namespace CL.WebLogic.Caching.Services;

/// <summary>
/// The provider behind WebLogic's response-compression middleware: ASP.NET Core's Brotli/gzip
/// provider wrapped in WebLogic's rules. Does nothing unless <c>Compression.Enabled</c> is on
/// (read per request), and never compresses a personal response (<see cref="WebResponsePrivacy"/>)
/// unless the route opted in with <c>AllowPersonalCompression()</c> — compressing secrets next to
/// reflected input leaks them through the size (BREACH). Responses that already carry a
/// <c>Content-Encoding</c> (the precompressed client bundle) are left alone.
/// </summary>
/// <remarks>
/// Kept separate from the app's own <see cref="IResponseCompressionProvider"/> registration so an
/// app that already calls <c>UseResponseCompression()</c> keeps its own options.
/// </remarks>
internal sealed class WebCompressionProvider : IResponseCompressionProvider
{
    private static readonly string[] ExtraMimeTypes = ["image/svg+xml", "application/problem+json"];

    private readonly WebLogicConfigHolder _config;
    private readonly IServiceProvider _services;
    private Compiled? _compiled;

    /// <summary>Creates the provider.</summary>
    public WebCompressionProvider(WebLogicConfigHolder config, IServiceProvider services)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _services = services ?? throw new ArgumentNullException(nameof(services));
    }

    /// <inheritdoc/>
    public bool CheckRequestAcceptsCompression(HttpContext context)
    {
        var config = _config.Current.Compression;
        return config.Enabled && Inner(config).CheckRequestAcceptsCompression(context);
    }

    /// <inheritdoc/>
    public ICompressionProvider? GetCompressionProvider(HttpContext context) =>
        Inner(_config.Current.Compression).GetCompressionProvider(context);

    /// <inheritdoc/>
    public bool ShouldCompressResponse(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var config = _config.Current;
        if (!config.Compression.Enabled)
            return false;

        // No body to compress (and a 304 must stay empty).
        var status = context.Response.StatusCode;
        if (status < StatusCodes.Status200OK || status is StatusCodes.Status204NoContent or StatusCodes.Status205ResetContent or StatusCodes.Status304NotModified)
            return false;

        var route = context.GetEndpoint()?.Metadata.GetMetadata<WebRouteMetadata>();
        if (route?.Compression == false)
            return false;
        // Static theme/plugin files hold nothing personal even when a signed-in user fetches them.
        var personalOk = route?.Compression == true || route?.Kind == WebRouteKind.Asset;
        if (!personalOk && WebResponsePrivacy.IsPersonal(context, config))
            return false;
        return Inner(config.Compression).ShouldCompressResponse(context);
    }

    // Rebuilt when a live config change swaps the section, so the level and MIME types follow it.
    private ResponseCompressionProvider Inner(CompressionConfig config)
    {
        var compiled = Volatile.Read(ref _compiled);
        if (compiled is not null && ReferenceEquals(compiled.Source, config))
            return compiled.Provider;

        var options = new ResponseCompressionOptions
        {
            EnableForHttps = true, // BREACH is handled by the personal-response rule above.
            MimeTypes = [.. ResponseCompressionDefaults.MimeTypes, .. ExtraMimeTypes, .. config.MimeTypes]
        };
        options.Providers.Add(new BrotliCompressionProvider(Options.Create(new BrotliCompressionProviderOptions { Level = config.Level })));
        options.Providers.Add(new GzipCompressionProvider(Options.Create(new GzipCompressionProviderOptions { Level = config.Level })));
        compiled = new Compiled(config, new ResponseCompressionProvider(_services, Options.Create(options)));
        Volatile.Write(ref _compiled, compiled);
        return compiled.Provider;
    }

    private sealed record Compiled(CompressionConfig Source, ResponseCompressionProvider Provider);
}
