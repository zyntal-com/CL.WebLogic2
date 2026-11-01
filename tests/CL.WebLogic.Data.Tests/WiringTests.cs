using CL.WebLogic.Auth.Accounts;
using CL.WebLogic.Auth.Users;
using CL.WebLogic.Hosting.Services;
using CL.WebLogic.Data.Hosting;
using CL.WebLogic.Data.Models;
using CL.WebLogic.Data.Services;
using CL.WebLogic.Data.Stores;
using CL.WebLogic.Security.Sessions;
using CL.WebLogic.Testing.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CL.WebLogic.Data.Tests;

/// <summary><c>UseDataStores</c> in a running WebLogic host.</summary>
[Collection(DataTestGroup.Name)]
public sealed class WiringTests(DataFixture fixture)
{
    [Theory, MemberData(nameof(DataFixture.Providers), MemberType = typeof(DataFixture))]
    public async Task UseDataStores_replaces_the_defaults_and_accounts_work_end_to_end(string provider)
    {
        _ = fixture;
        await using var host = await StartAsync(provider, o => o.Stores = WebDataStores.All);

        Assert.IsType<DataWebUserStore>(host.Services.GetRequiredService<IWebUserStore>());
        Assert.IsType<DataWebSessionStore>(host.Services.GetRequiredService<IWebSessionStore>());

        var name = "dora" + Guid.NewGuid().ToString("N")[..8];
        var created = await host.Services.GetRequiredService<WebAccountService>().RegisterAsync(new WebRegistration
        {
            UserName = name, Email = name + "@example.test", Password = "a long enough passphrase 42", EmailConfirmed = true
        }, default);
        Assert.True(created.IsSuccess, created.Error?.Message);
        Assert.NotNull(await host.Services.GetRequiredService<IWebUserStore>().FindByLoginAsync(name, default));

        var report = host.Services.GetRequiredService<ClusterReadinessCheck>().Evaluate();
        Assert.DoesNotContain(report.Errors, e => e.Contains("Store", StringComparison.Ordinal) && !e.Contains("Notifier", StringComparison.Ordinal) && !e.Contains("Backplane", StringComparison.Ordinal));
        Assert.DoesNotContain(report.Errors, e => e.StartsWith("DataProtection", StringComparison.Ordinal));

        Assert.True(await host.Services.GetServices<Microsoft.Extensions.Hosting.IHostedService>().OfType<WebDataSweepService>().Single().SweepOnceAsync(default) >= 0);
    }

    [Theory, MemberData(nameof(DataFixture.Providers), MemberType = typeof(DataFixture))]
    public async Task Stores_left_out_keep_the_app_defaults(string provider)
    {
        await using var host = await StartAsync(provider, o => o.Stores = WebDataStores.All & ~WebDataStores.Users);
        Assert.IsNotType<DataWebUserStore>(host.Services.GetRequiredService<IWebUserStore>());
        Assert.IsType<DataWebSessionStore>(host.Services.GetRequiredService<IWebSessionStore>());
    }

    private static Task<WebLogicTestHost> StartAsync(string provider, Action<WebDataOptions> configure) =>
        WebLogicTestHost.StartAsync(new WebLogicTestHostOptions
        {
            ConfigureWebLogic = web => web.UseInMemoryAccounts().UseDataStores(provider, configure),
            ConfigureServices = services =>
            {
                services.Replace(ServiceDescriptor.Singleton<IWebLibraryResolver, CodeLogicLibraryResolver>());
                services.AddDataProtection().SetApplicationName("data-wiring-" + provider).PersistKeysToDatabase(provider);
            }
        });
}
