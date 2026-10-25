using CL.WebLogic.Theming.Services;

namespace CL.WebLogic.Tests.Theming;

public sealed class ThemeAssetResolverTests
{
    [Theory]
    [InlineData("site.css")]
    [InlineData("img/logo.png")]
    [InlineData("vendor/bootstrap/bootstrap.min.css")]
    public void SafePaths_AreAccepted(string path) => Assert.True(ThemeAssetResolver.IsSafeRelativePath(path));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("../templates/home.html")]
    [InlineData("img/../../secret")]
    [InlineData("./site.css")]
    [InlineData(".git/config")]
    [InlineData("img/.env")]
    [InlineData("/etc/passwd")]
    [InlineData("C:/Windows/win.ini")]
    [InlineData("img\\logo.png")]
    [InlineData("site.css:stream")]
    [InlineData("img//logo.png")]
    [InlineData("site.css.")]
    [InlineData("site.css ")]
    [InlineData("a\u0000b.css")]
    public void UnsafePaths_AreRejected(string? path) => Assert.False(ThemeAssetResolver.IsSafeRelativePath(path));
}
