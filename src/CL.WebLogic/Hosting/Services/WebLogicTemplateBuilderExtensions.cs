using CL.WebLogic.Content.Models;
using CL.WebLogic.Content.Services;
using CL.WebLogic.Localization.Services;
using CL.WebLogic.Templates.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CL.WebLogic.Hosting.Services;

/// <summary>Template and content registrations on <see cref="WebLogicBuilder"/>.</summary>
public static class WebLogicTemplateBuilderExtensions
{
    /// <summary>Adds a custom template filter (<c>{model:x|name}</c>).</summary>
    public static WebLogicBuilder AddTemplateFilter<TFilter>(this WebLogicBuilder builder) where TFilter : class, ITemplateFilter
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Services.AddSingleton<ITemplateFilter, TFilter>();
        return builder;
    }

    /// <summary>Adds a provider of values every template can read.</summary>
    public static WebLogicBuilder AddTemplateGlobals<TProvider>(this WebLogicBuilder builder) where TProvider : class, ITemplateGlobalsProvider
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Services.AddSingleton<ITemplateGlobalsProvider, TProvider>();
        return builder;
    }

    /// <summary>Replaces the localizer used by <c>{t:key}</c> and form messages.</summary>
    public static WebLogicBuilder UseTextLocalizer<TLocalizer>(this WebLogicBuilder builder) where TLocalizer : class, IWebTextLocalizer
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Services.Replace(ServiceDescriptor.Singleton<IWebTextLocalizer, TLocalizer>());
        return builder;
    }

    /// <summary>
    /// Replaces the texts behind the default localizer (default: <c>{theme}/locales/{culture}.json</c>).
    /// Used only when <c>Localization.Enabled</c> is true.
    /// </summary>
    public static WebLogicBuilder UseTextResources<TResources>(this WebLogicBuilder builder) where TResources : class, IWebTextResources
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Services.Replace(ServiceDescriptor.Singleton<IWebTextResources, TResources>());
        return builder;
    }

    /// <summary>Replaces the group / permission checks used by <c>{if:group:…}</c> and <c>{if:permission:…}</c>.</summary>
    public static WebLogicBuilder UseTemplateAuthorizer<TAuthorizer>(this WebLogicBuilder builder) where TAuthorizer : class, ITemplateAuthorizer
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Services.Replace(ServiceDescriptor.Singleton<ITemplateAuthorizer, TAuthorizer>());
        return builder;
    }

    /// <summary>Configures the <c>|markdown</c> renderer.</summary>
    public static WebLogicBuilder UseMarkdownOptions(this WebLogicBuilder builder, WebMarkdownOptions options)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(options);
        builder.Services.Replace(ServiceDescriptor.Singleton<IWebMarkdownRenderer>(new WebMarkdownRenderer(options)));
        return builder;
    }

    /// <summary>Configures the <c>|sanitize</c> allowlists.</summary>
    public static WebLogicBuilder UseSanitizerOptions(this WebLogicBuilder builder, WebHtmlSanitizerOptions options)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(options);
        builder.Services.Replace(ServiceDescriptor.Singleton<IWebHtmlSanitizer>(new WebHtmlSanitizer(options)));
        return builder;
    }
}
