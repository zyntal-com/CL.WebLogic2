using CL.WebLogic.Configuration.Models;
using CL.WebLogic.Templates.Rendering;
using CL.WebLogic.Testing.Services;
using CL.WebLogic.Theming.Events;
using CL.WebLogic.Theming.Services;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace CL.WebLogic.Tests.Theming;

public sealed class ThemeSyncServiceTests
{
    [Fact]
    public async Task RemoteSource_SwitchesThemeRoot_AndPublishesEvent()
    {
        using var context = new FakeWebLogicContext(new WebLogicConfig { Theme = new ThemeConfig { Source = ThemeSourceKind.Git, RepositoryUrl = "https://x.test/t.git" } });
        var synced = Path.Combine(context.DataDirectory, "themes", "synced");
        var environment = new FakeEnvironment(context.DataDirectory);
        var root = new WebThemeRoot(context.Config, environment);
        var loader = new ThemeTemplateLoader(root, context.Config, new TemplateInterpreter(), new ClosureTemplateCompiler());
        ThemeSyncedEvent? published = null;
        context.Events.Subscribe<ThemeSyncedEvent>(e => published = e);

        using var service = new ThemeSyncService([new LocalThemeSource(environment), new FakeSource(ThemeSourceKind.Git, synced)], root, context.Config, loader, context);
        await service.StartAsync(CancellationToken.None);

        Assert.Equal(Path.GetFullPath(synced), root.RootPath);
        Assert.Equal("Git", published?.Source);
        Assert.NotNull(service.LastSyncedAt);
    }

    [Fact]
    public async Task MissingSource_FailsStartup()
    {
        using var context = new FakeWebLogicContext(new WebLogicConfig { Theme = new ThemeConfig { Source = ThemeSourceKind.Storage } });
        var environment = new FakeEnvironment(context.DataDirectory);
        var root = new WebThemeRoot(context.Config, environment);
        var loader = new ThemeTemplateLoader(root, context.Config, new TemplateInterpreter(), new ClosureTemplateCompiler());

        using var service = new ThemeSyncService([new LocalThemeSource(environment)], root, context.Config, loader, context);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.StartAsync(CancellationToken.None));
    }

    [Theory]
    [InlineData("", "weblogic-theme", "")]
    [InlineData("https://x.test/t.git", "../escape", "")]
    [InlineData("https://x.test/t.git", "ok", "../../etc")]
    public void GitConfig_IsValidated(string url, string id, string subPath)
    {
        var config = new WebLogicConfig { Theme = new ThemeConfig { Source = ThemeSourceKind.Git, RepositoryUrl = url, RepositoryId = id, ThemeSubPath = subPath } };
        Assert.False(config.Validate().IsValid);
    }

    private sealed class FakeSource(ThemeSourceKind kind, string path) : IWebThemeSource
    {
        public ThemeSourceKind Kind => kind;

        public Task<string> SyncAsync(ThemeConfig config, CancellationToken cancellationToken) => Task.FromResult(path);
    }

    private sealed class FakeEnvironment(string contentRoot) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";

        public string ApplicationName { get; set; } = "tests";

        public string ContentRootPath { get; set; } = contentRoot;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
