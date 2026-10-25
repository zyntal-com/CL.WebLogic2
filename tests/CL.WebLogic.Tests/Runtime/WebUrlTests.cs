using CL.WebLogic.Runtime.Services;

namespace CL.WebLogic.Tests.Runtime;

public sealed class WebUrlTests
{
    [Theory]
    [InlineData("/")]
    [InlineData("/admin")]
    [InlineData("/posts/hello-world?page=2#top")]
    public void IsLocal_AcceptsRootedPaths(string url) => Assert.True(WebUrl.IsLocal(url));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("admin")]
    [InlineData("//evil.example")]
    [InlineData("/\\evil.example")]
    [InlineData("/ok\\..\\x")]
    [InlineData("https://evil.example/")]
    [InlineData("javascript:alert(1)")]
    [InlineData("/path\r\nSet-Cookie: x=1")]
    [InlineData("/\tpath")]
    public void IsLocal_RejectsEverythingElse(string? url) => Assert.False(WebUrl.IsLocal(url));
}
