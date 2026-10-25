using CL.WebLogic.Configuration.Events;
using CL.WebLogic.Configuration.Models;
using CL.WebLogic.Configuration.Services;
using CL.WebLogic.Testing.Services;
using CodeLogic.Core.Logging;

namespace CL.WebLogic.Tests.Configuration;

public sealed class ConfigApplyCoordinatorTests
{
    [Fact]
    public async Task Apply_SwapsHolder_AndReportsChangedPaths()
    {
        using var ctx = new FakeWebLogicContext();
        WebLogicConfigAppliedEvent? applied = null;
        ctx.Events.Subscribe<WebLogicConfigAppliedEvent>(e => applied = e);
        var coordinator = new ConfigApplyCoordinator(ctx.Config, ctx.Logger, ctx.Events);

        var next = new WebLogicConfig { SiteName = "Changed" };
        var changed = await coordinator.ApplyAsync(next, CancellationToken.None);

        Assert.Same(next, ctx.Config.Current);
        Assert.Equal(1, ctx.Config.Revision);
        Assert.Contains("siteName", changed, StringComparer.OrdinalIgnoreCase);
        Assert.NotNull(applied);
        Assert.False(coordinator.RestartPending);
    }

    [Fact]
    public async Task Apply_NoChanges_DoesNotSwap()
    {
        using var ctx = new FakeWebLogicContext();
        var coordinator = new ConfigApplyCoordinator(ctx.Config, ctx.Logger, ctx.Events);

        var changed = await coordinator.ApplyAsync(new WebLogicConfig(), CancellationToken.None);

        Assert.Empty(changed);
        Assert.Equal(0, ctx.Config.Revision);
    }

    [Fact]
    public async Task Apply_LogsChangedPropertyNames_NotValues()
    {
        using var ctx = new FakeWebLogicContext();
        var coordinator = new ConfigApplyCoordinator(ctx.Config, ctx.Logger, ctx.Events);

        await coordinator.ApplyAsync(new WebLogicConfig { SiteName = "very-secret-value" }, CancellationToken.None);

        Assert.True(ctx.Log.Contains(LogLevel.Info, "siteName"));
        Assert.DoesNotContain(ctx.Log.Entries, e => e.Message.Contains("very-secret-value"));
    }
}
