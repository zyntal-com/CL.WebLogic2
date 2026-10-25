using CL.WebLogic.Hosting.Models;
using CL.WebLogic.Routing;
using CL.WebLogic.Routing.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CL.WebLogic.Hosting.Services;

/// <summary>
/// Fluent configuration passed to <c>AddCodeLogicWebLogic</c>. Every <c>UseXxx</c> / <c>AddXxx</c>
/// call is a plain DI registration — DI is the single source of app-supplied contracts.
/// </summary>
public sealed class WebLogicBuilder
{
    internal WebLogicBuilder(IServiceCollection services) =>
        Services = services ?? throw new ArgumentNullException(nameof(services));

    /// <summary>The application's service collection.</summary>
    public IServiceCollection Services { get; }

    /// <summary>Adds an application contributor resolved from DI (registered as a singleton).</summary>
    public WebLogicBuilder AddContributor<TContributor>(WebContributorDescriptor? descriptor = null)
        where TContributor : class, IWebContributor
    {
        Services.TryAddSingleton<TContributor>();
        Services.AddSingleton(new WebContributorRegistration { ContributorType = typeof(TContributor), Descriptor = descriptor });
        return this;
    }

    /// <summary>Adds a pre-built application contributor.</summary>
    public WebLogicBuilder AddContributor(IWebContributor contributor, WebContributorDescriptor? descriptor = null)
    {
        ArgumentNullException.ThrowIfNull(contributor);
        Services.AddSingleton(new WebContributorRegistration
        {
            ContributorType = contributor.GetType(),
            Instance = contributor,
            Descriptor = descriptor
        });
        return this;
    }

    /// <summary>Replaces the error page renderer (e.g. with a themed one).</summary>
    public WebLogicBuilder UseErrorPageRenderer<TRenderer>() where TRenderer : class, IWebErrorPageRenderer
    {
        Services.Replace(ServiceDescriptor.Singleton<IWebErrorPageRenderer, TRenderer>());
        return this;
    }

    /// <summary>Stops the CodeLogic runtime when the host shuts down (do not also call <c>CodeLogic.StopAsync()</c>).</summary>
    public WebLogicBuilder StopCodeLogicOnShutdown()
    {
        Services.AddHostedService<CodeLogicLifetimeBridge>();
        return this;
    }
}
