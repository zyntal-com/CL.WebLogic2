using CL.WebLogic.Auth.Accounts;
using CL.WebLogic.Auth.Users;
using CL.WebLogic.Hosting.Services;
using CL.WebLogic.MySql.Hosting;
using CL.WebLogic.MySql.Models;
using CL.WebLogic.MySql.Services;
using CL.WebLogic.MySql.Stores;
using CL.WebLogic.Security.Sessions;
using CL.WebLogic.Testing.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CL.WebLogic.MySql.Tests;

/// <summary><c>UseMySqlStores</c> in a running WebLogic host.</summary>
[Collection(MySqlTestGroup.Name)]
public sealed class WiringTests(MySqlFixture fixture)
{
    [MySqlFact]
    public async Task UseMySqlStores_replaces_the_defaults_and_accounts_work_end_to_end()
    {
        _ = fixture;
        await using var host = await StartAsync(o => o.Stores = WebMySqlStores.All);

        Assert.IsType<MySqlWebUserStore>(host.Services.GetRequiredService<IWebUserStore>());
        Assert.IsType<MySqlWebSessionStore>(host.Services.GetRequiredService<IWebSessionStore>());

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

        Assert.True(await host.Services.GetServices<Microsoft.Extensions.Hosting.IHostedService>().OfType<WebMySqlSweepService>().Single().SweepOnceAsync(default) >= 0);
    }

    [MySqlFact]
    public async Task Stores_left_out_keep_the_app_defaults()
    {
        await using var host = await StartAsync(o => o.Stores = WebMySqlStores.All & ~WebMySqlStores.Users);
        Assert.IsNotType<MySqlWebUserStore>(host.Services.GetRequiredService<IWebUserStore>());
        Assert.IsType<MySqlWebSessionStore>(host.Services.GetRequiredService<IWebSessionStore>());
    }

    private static Task<WebLogicTestHost> StartAsync(Action<WebMySqlOptions> configure) =>
        WebLogicTestHost.StartAsync(new WebLogicTestHostOptions
        {
            ConfigureWebLogic = web => web.UseInMemoryAccounts().UseMySqlStores("Default", configure),
            ConfigureServices = services =>
            {
                services.Replace(ServiceDescriptor.Singleton<IWebLibraryResolver, CodeLogicLibraryResolver>());
                services.AddDataProtection().SetApplicationName("mysql-wiring").PersistKeysToMySql();
            }
        });
}
