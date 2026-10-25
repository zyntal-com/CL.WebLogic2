using System.Net;
using CL.WebLogic.Results;
using CL.WebLogic.Routing;
using CL.WebLogic.Routing.Services;
using CL.WebLogic.Testing.Services;
using CodeLogic.Core.Events;
using CodeLogic.Framework.Application.Plugins;
using CodeLogic.Framework.Libraries;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CL.WebLogic.IntegrationTests.Hosting;

public sealed class PluginContributionTests
{
    [Fact]
    public async Task PluginLoadAndUnload_AddsAndRemovesRoutes()
    {
        var source = new FakePluginSource();
        await using var host = await WebLogicTestHost.StartAsync(new WebLogicTestHostOptions
        {
            ConfigureServices = services => services.Replace(ServiceDescriptor.Singleton<IWebPluginSource>(source))
        });

        Assert.Equal(HttpStatusCode.NotFound, (await host.Client.GetAsync("/plugin/hello")).StatusCode);

        source.Plugin = new WebPlugin();
        await host.Context.Events.PublishAsync(new PluginLoadedEvent("demo.plugin", "Demo"));

        Assert.Equal("hello from plugin", await host.Client.GetStringAsync("/plugin/hello"));

        await host.Context.Events.PublishAsync(new PluginUnloadedEvent("demo.plugin", "Demo"));

        Assert.Equal(HttpStatusCode.NotFound, (await host.Client.GetAsync("/plugin/hello")).StatusCode);
    }

    [Fact]
    public async Task FailingPlugin_IsLogged_AndDoesNotCrashHost()
    {
        var source = new FakePluginSource { Plugin = new WebPlugin(fail: true) };
        await using var host = await WebLogicTestHost.StartAsync(new WebLogicTestHostOptions
        {
            ConfigureServices = services => services.Replace(ServiceDescriptor.Singleton<IWebPluginSource>(source))
        });

        Assert.Contains(host.Context.Log.Entries, e => e.Message.Contains("failed to register"));
        Assert.Equal(HttpStatusCode.OK, (await host.Client.GetAsync("/_weblogic/health/live")).StatusCode);
    }

    private sealed class FakePluginSource : IWebPluginSource
    {
        public WebPlugin? Plugin { get; set; }

        public IReadOnlyList<IPlugin> GetLoadedPlugins() => Plugin is null ? [] : [Plugin];

        public IPlugin? GetPlugin(string pluginId) => Plugin?.Manifest.Id == pluginId ? Plugin : null;
    }

    private sealed class WebPlugin(bool fail = false) : IPlugin, IWebContributor
    {
        public PluginManifest Manifest { get; } = new() { Id = "demo.plugin", Name = "Demo", Version = "1.0.0" };

        public PluginState State => PluginState.Started;

        public Task ContributeAsync(WebContributionBuilder builder, CancellationToken cancellationToken)
        {
            if (fail) throw new InvalidOperationException("plugin bug");
            builder.MapPage("/plugin/hello", (_, _) => ValueTask.FromResult<WebResult>(WebResult.Text("hello from plugin"))).AllowAnonymous();
            return Task.CompletedTask;
        }

        public Task OnConfigureAsync(PluginContext context) => Task.CompletedTask;
        public Task OnInitializeAsync(PluginContext context) => Task.CompletedTask;
        public Task OnStartAsync(PluginContext context) => Task.CompletedTask;
        public Task OnUnloadAsync() => Task.CompletedTask;
        public Task<HealthStatus> HealthCheckAsync() => Task.FromResult(HealthStatus.Healthy());
        public void Dispose() { }
    }
}
