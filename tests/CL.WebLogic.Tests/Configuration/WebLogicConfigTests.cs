using CL.WebLogic.Configuration.Models;

namespace CL.WebLogic.Tests.Configuration;

public sealed class WebLogicConfigTests
{
    [Fact]
    public void Defaults_AreValid() =>
        Assert.True(new WebLogicConfig().Validate().IsValid);

    [Theory]
    [InlineData("ftp://example.com")]
    [InlineData("not a url")]
    [InlineData("/relative")]
    public void PublicBaseUrl_MustBeAbsoluteHttp(string value)
    {
        var result = new WebLogicConfig { PublicBaseUrl = value }.Validate();
        Assert.False(result.IsValid);
    }

    [Fact]
    public void SiteName_IsRequired() =>
        Assert.False(new WebLogicConfig { SiteName = " " }.Validate().IsValid);
}
