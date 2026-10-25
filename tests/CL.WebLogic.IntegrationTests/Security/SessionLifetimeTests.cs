using System.Net;
using CL.WebLogic.Testing.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CL.WebLogic.IntegrationTests.Security;

public sealed class SessionLifetimeTests
{
    private static Task<WebLogicTestHost> StartAsync(ManualTimeProvider clock, Action<CL.WebLogic.Configuration.Models.WebLogicConfig> configure) =>
        SecurityTestSite.StartAsync(configure, options: new WebLogicTestHostOptions
        {
            ConfigureServices = services => services.Replace(ServiceDescriptor.Singleton<TimeProvider>(clock))
        });

    [Fact]
    public async Task IdleSessions_Expire()
    {
        var clock = new ManualTimeProvider();
        await using var host = await StartAsync(clock, c => c.Security.Session.IdleTimeoutMinutes = 10);
        await host.Client.GetAsync("/signin?user=alice");

        clock.Advance(TimeSpan.FromMinutes(5));
        Assert.Equal(HttpStatusCode.OK, (await host.Client.GetAsync("/api/secure")).StatusCode); // activity extends the session

        clock.Advance(TimeSpan.FromMinutes(9));
        Assert.Equal(HttpStatusCode.OK, (await host.Client.GetAsync("/api/secure")).StatusCode);

        clock.Advance(TimeSpan.FromMinutes(11));
        Assert.Equal(HttpStatusCode.Unauthorized, (await host.Client.GetAsync("/api/secure")).StatusCode);
    }

    [Fact]
    public async Task AbsoluteTimeout_EndsActiveSessions()
    {
        var clock = new ManualTimeProvider();
        await using var host = await StartAsync(clock, c =>
        {
            c.Security.Session.IdleTimeoutMinutes = 60;
            c.Security.Session.AbsoluteTimeoutHours = 2;
        });
        await host.Client.GetAsync("/signin?user=alice");

        for (var i = 0; i < 3; i++)
        {
            clock.Advance(TimeSpan.FromMinutes(50));
            await host.Client.GetAsync("/api/secure");
        }

        Assert.Equal(HttpStatusCode.Unauthorized, (await host.Client.GetAsync("/api/secure")).StatusCode);
    }
}
