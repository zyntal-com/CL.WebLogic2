using CL.WebLogic.Seo.Models;
using CL.WebLogic.Seo.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CL.WebLogic.Hosting.Services;

/// <summary>Sitemap and robots.txt registration on <see cref="WebLogicBuilder"/> (off unless called).</summary>
public static class WebLogicSeoBuilderExtensions
{
    /// <summary>
    /// Serves <c>/sitemap.xml</c> (anonymous GET page routes without parameters, plus the entries of
    /// <see cref="IWebSitemapProvider"/>) and <c>/robots.txt</c>. URLs are absolute from <c>PublicBaseUrl</c>;
    /// without it the sitemap answers 404. Calling it again only applies <paramref name="configure"/> again.
    /// </summary>
    public static WebLogicBuilder AddSitemap(this WebLogicBuilder builder, Action<WebSitemapOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        var existing = builder.Services.FirstOrDefault(d => d.ServiceType == typeof(WebSitemapOptions))?.ImplementationInstance as WebSitemapOptions;
        var options = existing ?? new WebSitemapOptions();
        configure?.Invoke(options);
        options.EnsureValid();
        if (existing is not null)
            return builder;

        builder.Services.AddSingleton(options);
        builder.Services.TryAddSingleton<IWebSitemapProvider, NullSitemapProvider>();
        builder.Services.TryAddSingleton<WebSitemapService>();
        return builder.AddContributor<SitemapContributor>(SitemapContributor.Descriptor);
    }

    /// <summary>Supplies the dynamic sitemap entries (posts, products, …). Use together with <see cref="AddSitemap"/>.</summary>
    public static WebLogicBuilder UseSitemapProvider<TProvider>(this WebLogicBuilder builder) where TProvider : class, IWebSitemapProvider
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Services.Replace(ServiceDescriptor.Singleton<IWebSitemapProvider, TProvider>());
        return builder;
    }
}
