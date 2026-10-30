using CL.WebLogic.Configuration.Models;

namespace CL.WebLogic.Tests.Localization;

public sealed class LocalizationConfigTests
{
    private static bool IsValid(Action<LocalizationConfig> configure)
    {
        var config = new WebLogicConfig();
        configure(config.Localization);
        return config.Validate().IsValid;
    }

    [Fact]
    public void Defaults_are_off_and_valid()
    {
        Assert.False(new WebLogicConfig().Localization.Enabled);
        Assert.True(IsValid(_ => { }));
        Assert.True(IsValid(l => { l.Enabled = true; l.SupportedCultures = ["sv-SE", "da"]; }));
    }

    [Theory]
    [InlineData("xx-YY")]
    [InlineData("sv;x")]
    [InlineData("")]
    public void Enabled_needs_known_culture_names(string name)
    {
        Assert.False(IsValid(l => { l.Enabled = true; l.SupportedCultures = [name]; }));
        Assert.False(IsValid(l => { l.Enabled = true; l.DefaultCulture = name; }));
    }

    [Fact]
    public void Sources_paths_and_names_are_checked()
    {
        Assert.False(IsValid(l => l.Sources = ["query", "ip"]));
        Assert.False(IsValid(l => l.ResourcesFolder = "../secrets"));
        Assert.False(IsValid(l => l.SwitchPath = "//evil.example"));
        Assert.False(IsValid(l => l.CookieName = "a b"));
        Assert.False(IsValid(l => l.QueryParameter = ""));
    }
}
