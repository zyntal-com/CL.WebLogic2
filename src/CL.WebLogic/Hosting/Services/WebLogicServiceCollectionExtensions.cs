using CL.WebLogic.Configuration.Services;
using CL.WebLogic.Content.Services;
using CL.WebLogic.Diagnostics.Services;
using CL.WebLogic.Localization.Services;
using CL.WebLogic.Routing.Services;
using CL.WebLogic.Security.Headers;
using CL.WebLogic.Templates.Rendering;
using CL.WebLogic.Theming.Services;
using CodeLogic;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace CL.WebLogic.Hosting.Services;

/// <summary>Registers WebLogic with an ASP.NET Core host.</summary>
public static class WebLogicServiceCollectionExtensions
{
    /// <summary>
    /// Adds WebLogic using the loaded <see cref="WebLogicLibrary"/>. Load and start CodeLogic
    /// (with <c>WebLogicLibrary</c>) before the host is built.
    /// </summary>
    public static WebLogicBuilder AddCodeLogicWebLogic(this IHostApplicationBuilder builder, Action<WebLogicBuilder>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.Services.AddCodeLogicWebLogic(configure);
    }

    /// <summary>Adds WebLogic using the loaded <see cref="WebLogicLibrary"/>.</summary>
    public static WebLogicBuilder AddCodeLogicWebLogic(this IServiceCollection services, Action<WebLogicBuilder>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<IWebLogicContext>(_ => ResolveLibraryContext());
        services.TryAddSingleton<IWebPluginSource, CodeLogicPluginSource>();
        services.TryAddSingleton<IWebLibraryResolver, CodeLogicLibraryResolver>();
        return AddCore(services, configure);
    }

    /// <summary>
    /// Adds WebLogic over an explicit <see cref="IWebLogicContext"/> — used by tests and by hosts
    /// that run WebLogic without the static CodeLogic runtime. No plugins are discovered.
    /// </summary>
    public static WebLogicBuilder AddCodeLogicWebLogic(this IServiceCollection services, IWebLogicContext context, Action<WebLogicBuilder>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(context);
        services.TryAddSingleton(context);
        services.TryAddSingleton<IWebPluginSource, EmptyPluginSource>();
        services.TryAddSingleton<IWebLibraryResolver, EmptyLibraryResolver>();
        return AddCore(services, configure);
    }

    private static WebLogicBuilder AddCore(IServiceCollection services, Action<WebLogicBuilder>? configure)
    {
        services.TryAddSingleton<WebLogicConfigHolder>(sp => sp.GetRequiredService<IWebLogicContext>().Config);

        // Routing
        services.TryAddSingleton<WebRouteRegistry>();
        services.TryAddSingleton<ContributorRegistrar>();
        services.TryAddSingleton<WebLogicEndpointDataSource>();

        // Hosting
        services.TryAddSingleton<DefaultErrorPageRenderer>();
        services.TryAddSingleton<IWebErrorPageRenderer, ThemedErrorPageRenderer>();
        services.AddHostedService(sp => sp.GetRequiredService<ThemeSyncService>());
        services.AddHostedService<ContributorBootstrapper>();
        services.AddHostedService<PluginContributionWatcher>();
        services.AddHealthChecks().AddCheck<WebLogicHealthCheck>("weblogic", tags: ["ready"]);
        services.AddCors();

        // Diagnostics: metrics and spans cost a flag check until a listener (OpenTelemetry) attaches.
        services.AddMetrics();
        services.TryAddSingleton<WebLogicMetrics>();
        services.AddHostedService<WebLogicMetricsRecorder>();

        // Theming
        services.TryAddSingleton<WebThemeRoot>();
        services.TryAddSingleton<IWebThemeRoot>(sp => sp.GetRequiredService<WebThemeRoot>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IWebThemeSource, LocalThemeSource>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IWebThemeSource, GitThemeSource>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IWebThemeSource, StorageThemeSource>());
        services.TryAddSingleton<ThemeSyncService>();
        services.TryAddSingleton<ThemeAssetResolver>();
        services.TryAddSingleton<ThemeAssetContributor>();
        services.TryAddSingleton<PluginAssetContributor>();
        services.TryAddSingleton<WebContributedAssets>();

        WebLogicSecurityRegistration.AddSecurity(services);
        WebLogicAuthRegistration.AddAuth(services);
        WebLogicFormsRegistration.AddForms(services);
        WebLogicFeaturesRegistration.AddFeatures(services);
        AddTemplates(services);

        var builder = new WebLogicBuilder(services);
        configure?.Invoke(builder);
        return builder;
    }

    private static void AddTemplates(IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<TemplateInterpreter>();
        services.TryAddSingleton<ClosureTemplateCompiler>();
        services.TryAddSingleton<WebContributedTemplates>();
        services.TryAddSingleton<ThemeTemplateLoader>();
        services.TryAddSingleton<ITemplateLoader>(sp => sp.GetRequiredService<ThemeTemplateLoader>());
        services.TryAddSingleton<TemplateFilterRegistry>();
        services.TryAddSingleton<Client.Services.ClientAssetCatalog>(_ => new Client.Services.ClientAssetCatalog());
        services.TryAddSingleton<Runtime.Services.WebFlash>();
        services.TryAddSingleton<Client.Services.ClientScriptProvider>();
        services.TryAddSingleton<Client.Services.ClientContributor>();
        services.TryAddSingleton<ITemplateScriptProvider>(sp => sp.GetRequiredService<Client.Services.ClientScriptProvider>());
        services.TryAddSingleton<ITemplateAssetUrlResolver, ThemeAssetUrlResolver>();
        services.TryAddSingleton<IWebTextLocalizer, PassthroughTextLocalizer>();
        services.TryAddSingleton<ICspNonceProvider, CspNonceProvider>();
        services.TryAddSingleton<IWebMarkdownRenderer>(_ => new WebMarkdownRenderer());
        services.TryAddSingleton<IWebHtmlSanitizer>(_ => new WebHtmlSanitizer());
        services.TryAddSingleton<TemplateRenderServices>();
        services.TryAddSingleton<IWebTemplateRenderer, WebTemplateRenderer>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<ITemplateGlobalsProvider, ConfigTemplateGlobalsProvider>());
    }

    private static IWebLogicContext ResolveLibraryContext() =>
        Libraries.Get<WebLogicLibrary>()?.Context
        ?? throw new InvalidOperationException(
            "CL.WebLogic is not running. Load it with 'await Libraries.LoadAsync<WebLogicLibrary>()' and call " +
            "'await CodeLogic.ConfigureAsync()' / 'await CodeLogic.StartAsync()' before building the web host.");
}
