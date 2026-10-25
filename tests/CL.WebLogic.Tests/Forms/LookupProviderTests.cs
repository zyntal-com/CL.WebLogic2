using CL.WebLogic.Forms.Providers;
using CL.WebLogic.Testing.Services;
using CodeLogic.Core.Logging;

namespace CL.WebLogic.Tests.Forms;

public sealed class LookupProviderTests
{
    [Theory]
    [InlineData("http://api.example.com/search?q={term}")]
    [InlineData("/search?q={term}")]
    [InlineData("ftp://api.example.com/{term}")]
    public void Lookup_urls_must_be_absolute_https(string url)
    {
        using var context = new FakeWebLogicContext();
        Assert.Throws<ArgumentException>(() => new WebFormHttpLookupProvider("lookup", url, context.Config, context));
    }

    [Fact]
    public async Task Hosts_outside_the_allowlist_are_never_called()
    {
        // Regression: the old provider built lookup URLs from the request's Host header (SSRF).
        using var context = new FakeWebLogicContext();
        var provider = new WebFormHttpLookupProvider("lookup", "https://169.254.169.254/latest/{term}", context.Config, context);

        var options = await provider.SearchAsync("meta-data", new WebFormOptionsRequest(), CancellationToken.None);

        Assert.Empty(options);
        Assert.True(context.Log.Contains(LogLevel.Warning, "not in Forms.AllowedLookupHosts"));
    }

    [Fact]
    public async Task Static_provider_searches_and_checks_validity()
    {
        var provider = new StaticOptionsProvider("colors", [new("red", "Red"), new("green", "Green"), new("gone", "Gone", Disabled: true)]);

        Assert.Equal(["green"], (await provider.SearchAsync("gre", new WebFormOptionsRequest(), CancellationToken.None)).Select(o => o.Value));
        Assert.True(await provider.IsValidAsync("RED", new WebFormOptionsRequest(), CancellationToken.None));
        Assert.False(await provider.IsValidAsync("gone", new WebFormOptionsRequest(), CancellationToken.None));
        Assert.False(await provider.IsValidAsync("blue", new WebFormOptionsRequest(), CancellationToken.None));
    }
}
