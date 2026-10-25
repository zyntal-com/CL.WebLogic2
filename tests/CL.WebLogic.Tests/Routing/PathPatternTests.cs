using CL.WebLogic.Routing.Services;

namespace CL.WebLogic.Tests.Routing;

public sealed class PathPatternTests
{
    [Theory]
    [InlineData("/blog/**", "/blog/2026/post", true)]
    [InlineData("/blog/**", "/blog/", true)]
    [InlineData("/blog/*", "/blog/2026/post", false)]
    [InlineData("/docs/*/edit", "/docs/intro/edit", true)]
    [InlineData("/docs/*/edit", "/docs/a/b/edit", false)]
    [InlineData("/", "/", true)]
    [InlineData("/ADMIN/**", "/admin/users", true)]
    [InlineData("/a.b", "/axb", false)]
    public void Globs_match_paths(string pattern, string path, bool expected) => Assert.Equal(expected, WebPathPattern.IsMatch(pattern, path));

    [Fact]
    public void Include_and_exclude_lists()
    {
        Assert.True(WebPathPattern.Passes("/x", [], []));
        Assert.True(WebPathPattern.Passes("/blog/a", ["/blog/**"], ["/blog/drafts/**"]));
        Assert.False(WebPathPattern.Passes("/blog/drafts/a", ["/blog/**"], ["/blog/drafts/**"]));
        Assert.False(WebPathPattern.Passes("/shop", ["/blog/**"], []));
    }
}
