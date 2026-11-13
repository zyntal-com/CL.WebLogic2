using CL.WebLogic.Client.Services;
using CL.WebLogic.Configuration.Models;
using CL.WebLogic.Configuration.Services;
using CL.WebLogic.Runtime.Models;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace CL.WebLogic.Tests.Client;

public sealed class ClientScriptProviderTests
{
    private sealed class Env : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;
        public string ApplicationName { get; set; } = "test";
        public string ContentRootPath { get; set; } = ".";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    [Fact]
    public void ConfigJson_is_reused_until_the_configuration_is_swapped()
    {
        var holder = new WebLogicConfigHolder(new WebLogicConfig());
        var provider = new ClientScriptProvider(new ClientAssetCatalog(), holder, new Env());

        var first = provider.ConfigJson("", true, "layouts/main");
        Assert.Same(first, provider.ConfigJson("", true, "layouts/main"));
        Assert.NotEqual(first, provider.ConfigJson("/app", true, "layouts/main"));
        Assert.NotEqual(first, provider.ConfigJson("", false, "layouts/main"));

        var next = new WebLogicConfig();
        next.Security.Csrf.HeaderName = "X-Other-Csrf";
        holder.Swap(next);

        var swapped = provider.ConfigJson("", true, "layouts/main");
        Assert.NotEqual(first, swapped);
        Assert.Contains("X-Other-Csrf", swapped, StringComparison.Ordinal);
    }

    [Fact]
    public void ConfigJson_with_a_flash_message_is_never_cached()
    {
        var provider = new ClientScriptProvider(new ClientAssetCatalog(), new WebLogicConfigHolder(new WebLogicConfig()), new Env());

        var plain = provider.ConfigJson("", true, null);
        var flashed = provider.ConfigJson("", true, null, new WebFlashMessage("Saved", "success"));

        Assert.Contains("Saved", flashed, StringComparison.Ordinal);
        Assert.DoesNotContain("Saved", provider.ConfigJson("", true, null), StringComparison.Ordinal);
        Assert.Same(plain, provider.ConfigJson("", true, null));
    }
}
