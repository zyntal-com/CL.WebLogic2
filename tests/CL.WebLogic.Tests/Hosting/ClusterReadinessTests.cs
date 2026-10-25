using System.Reflection;
using CL.WebLogic.Configuration.Models;
using CL.WebLogic.Configuration.Services;
using CL.WebLogic.Hosting.Services;
using CL.WebLogic.Security.RateLimiting;
using CL.WebLogic.Security.Sessions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace CL.WebLogic.Tests.Hosting;

public sealed class ClusterReadinessTests
{
    /// <summary>Stands in for an app's shared store; never invoked by the check.</summary>
    public class SharedStore : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => throw new NotSupportedException();
    }

    private sealed class Environment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Production";
        public string ApplicationName { get; set; } = "app";
        public string ContentRootPath { get; set; } = Path.GetTempPath();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private static object Shared(Type contract) =>
        typeof(DispatchProxy).GetMethod(nameof(DispatchProxy.Create), 2, [])!.MakeGenericMethod(contract, typeof(SharedStore)).Invoke(null, null)!;

    private static ClusterReadinessCheck Check(Action<IServiceCollection>? register = null, Action<WebLogicConfig>? configure = null, bool persistKeys = true)
    {
        var config = new WebLogicConfig();
        configure?.Invoke(config);
        var services = new ServiceCollection();
        var dataProtection = services.AddDataProtection();
        if (persistKeys)
            dataProtection.PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(Path.GetTempPath(), "wl-cluster-keys"))).SetApplicationName("app");
        foreach (var (contract, _) in ClusterReadinessCheck.SharedContracts.Concat(ClusterReadinessCheck.AdvisedContracts))
            services.AddSingleton(contract, Shared(contract));
        register?.Invoke(services);
        var provider = services.BuildServiceProvider();
        return new ClusterReadinessCheck(provider, new WebLogicConfigHolder(config), new Environment());
    }

    [Fact]
    public void Shared_stores_and_persisted_keys_are_ready()
    {
        var report = Check().Evaluate();
        Assert.True(report.IsReady, string.Join("; ", report.Errors));
        Assert.Empty(report.Warnings);
    }

    [Fact]
    public void In_memory_defaults_block_clustering()
    {
        var report = Check(s =>
        {
            s.AddSingleton<IWebSessionStore>(new InMemoryWebSessionStore());
            s.AddSingleton<IWebRateLimitStore>(sp => new InMemoryRateLimitStore(new WebLogicConfigHolder(new WebLogicConfig())));
        }).Evaluate();

        Assert.False(report.IsReady);
        Assert.Contains(report.Errors, e => e.StartsWith("IWebSessionStore → InMemoryWebSessionStore", StringComparison.Ordinal));
        Assert.Contains(report.Errors, e => e.StartsWith("IWebRateLimitStore → InMemoryRateLimitStore", StringComparison.Ordinal));
    }

    [Fact]
    public void Allowed_node_local_contracts_become_warnings()
    {
        var report = Check(
            s => s.AddSingleton<IWebRateLimitStore>(sp => new InMemoryRateLimitStore(new WebLogicConfigHolder(new WebLogicConfig()))),
            c => c.Cluster.AllowNodeLocal.Add("IWebRateLimitStore")).Evaluate();

        Assert.True(report.IsReady);
        Assert.Contains(report.Warnings, w => w.Contains("allowed by Cluster.AllowNodeLocal", StringComparison.Ordinal));
    }

    [Fact]
    public void Unpersisted_data_protection_keys_block_clustering()
    {
        var report = Check(persistKeys: false).Evaluate();
        Assert.Contains(report.Errors, e => e.StartsWith("DataProtection keys are not persisted", StringComparison.Ordinal));
        Assert.Contains(report.Warnings, w => w.Contains("SetApplicationName", StringComparison.Ordinal));
    }
}
