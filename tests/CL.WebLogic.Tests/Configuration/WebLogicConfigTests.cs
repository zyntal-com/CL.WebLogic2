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

    [Theory]
    [InlineData("", true)]
    [InlineData("https://cdn.example.com/theme/assets", true)]
    [InlineData("http://localhost:9000/bucket/assets/", true)]
    [InlineData("/assets", false)]
    [InlineData("ftp://cdn.example.com/assets", false)]
    [InlineData("https://cdn.example.com/assets?x=1", false)]
    [InlineData("https://user:pw@cdn.example.com/assets", false)]
    public void AssetsBaseUrl_MustBeAbsoluteHttpWithoutQuery(string value, bool valid) =>
        Assert.Equal(valid, new WebLogicConfig { Theme = new ThemeConfig { AssetsBaseUrl = value } }.Validate().IsValid);

    [Fact]
    public void AssetsOrigin_IsSchemeHostAndPort()
    {
        Assert.Equal("https://cdn.example.com", ThemeConfig.AssetsOrigin("https://cdn.example.com/theme/assets"));
        Assert.Equal("http://localhost:9000", ThemeConfig.AssetsOrigin("http://localhost:9000/b/assets"));
        Assert.Null(ThemeConfig.AssetsOrigin(""));
    }

    [Fact]
    public void SiteName_IsRequired() =>
        Assert.False(new WebLogicConfig { SiteName = " " }.Validate().IsValid);

    [Fact]
    public void Compression_and_etags_are_off_by_default()
    {
        var config = new WebLogicConfig();
        Assert.False(config.Compression.Enabled);
        Assert.False(config.Cache.ETags);
    }

    [Theory]
    [InlineData("application/manifest+json", true)]
    [InlineData("text", false)]
    [InlineData("text/ html", false)]
    public void Compression_mime_types_are_validated(string mimeType, bool valid)
    {
        var config = new WebLogicConfig();
        config.Compression.MimeTypes.Add(mimeType);
        Assert.Equal(valid, config.Validate().IsValid);
    }
}
