using System.Collections.Concurrent;
using System.Net;
using CL.WebLogic.Security.Audit;
using CL.WebLogic.Security.Events;
using CL.WebLogic.Testing.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace CL.WebLogic.IntegrationTests.Security;

public sealed class PipelineTests
{
    [Fact]
    public async Task DevelopmentStores_AreRefusedInProduction()
    {
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => SecurityTestSite.StartAsync(
            options: new WebLogicTestHostOptions { EnvironmentName = "Production", AllowDevelopmentStores = false }));

        Assert.Contains("IWebSessionStore", error.Message);
    }

    [Fact]
    public async Task DisabledStage_IsSkipped_AndLogged()
    {
        await using var host = await SecurityTestSite.StartAsync(c => c.Security.DisabledStages.Add("antiforgery"));

        Assert.Equal(HttpStatusCode.OK, (await host.Client.PostAsync("/submit", new StringContent("{}"))).StatusCode);
        Assert.Contains(host.Context.Log.Entries, e => e.Message.Contains("'antiforgery' is disabled"));
    }

    [Fact]
    public async Task Rejections_AreAudited_AndPublished()
    {
        using var sink = new CapturingSink();
        await using var host = await SecurityTestSite.StartAsync(web: w => w.Services.AddSingletonSink(sink));
        WebSecurityRejectedEvent? published = null;
        host.Context.Events.Subscribe<WebSecurityRejectedEvent>(e => published = e);

        await host.Client.PostAsync("/submit", new StringContent("{}"));

        var entry = await sink.WaitAsync();
        Assert.Equal("security.antiforgery", entry.Category);
        Assert.Equal("csrf-token-missing", entry.Outcome);
        Assert.Equal(64, entry.IpHash?.Length); // hashed, never the raw address
        Assert.Equal("antiforgery", published?.Stage);
    }

    [Fact]
    public async Task MinimalApis_CanUseAspNetAuthorization()
    {
        await using var host = await SecurityTestSite.StartAsync(options: new WebLogicTestHostOptions
        {
            ConfigureApp = app => app.MapGet("/minimal", (HttpContext c) => "hi " + c.User.Identity!.Name).RequireAuthorization()
        });

        Assert.Equal(HttpStatusCode.Unauthorized, (await host.Client.GetAsync("/minimal")).StatusCode);
        await host.Client.GetAsync("/signin?user=alice");
        Assert.Equal(HttpStatusCode.OK, (await host.Client.GetAsync("/minimal")).StatusCode);
    }

    private sealed class CapturingSink : IWebAuditSink, IDisposable
    {
        private readonly ConcurrentQueue<WebAuditEntry> _entries = new();
        private readonly SemaphoreSlim _signal = new(0);

        public ValueTask WriteAsync(IReadOnlyList<WebAuditEntry> entries, CancellationToken cancellationToken)
        {
            foreach (var entry in entries)
            {
                _entries.Enqueue(entry);
                _signal.Release();
            }
            return ValueTask.CompletedTask;
        }

        public void Dispose() => _signal.Dispose();

        public async Task<WebAuditEntry> WaitAsync()
        {
            Assert.True(await _signal.WaitAsync(TimeSpan.FromSeconds(5)), "no audit entry arrived");
            _entries.TryDequeue(out var entry);
            return entry!;
        }
    }
}

internal static class SinkRegistration
{
    public static void AddSingletonSink(this Microsoft.Extensions.DependencyInjection.IServiceCollection services, IWebAuditSink sink) =>
        Microsoft.Extensions.DependencyInjection.Extensions.ServiceCollectionDescriptorExtensions.Replace(services,
            Microsoft.Extensions.DependencyInjection.ServiceDescriptor.Singleton(sink));
}
