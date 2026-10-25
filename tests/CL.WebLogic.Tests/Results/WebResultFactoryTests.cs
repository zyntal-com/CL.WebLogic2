using CL.WebLogic.Results;

namespace CL.WebLogic.Tests.Results;

public sealed class WebResultFactoryTests
{
    [Theory]
    [InlineData("//evil.example")]
    [InlineData("https://evil.example")]
    [InlineData("relative")]
    public void Redirect_RejectsNonLocalUrls(string url) =>
        Assert.Throws<ArgumentException>(() => WebResult.Redirect(url));

    [Fact]
    public void Redirect_StatusCodes()
    {
        Assert.Equal(302, WebResult.Redirect("/a").StatusCode);
        Assert.Equal(301, WebResult.Redirect("/a", permanent: true).StatusCode);
        Assert.Equal(307, WebResult.Redirect("/a", preserveMethod: true).StatusCode);
        Assert.Equal(308, WebResult.Redirect("/a", permanent: true, preserveMethod: true).StatusCode);
    }

    [Fact]
    public void ExternalRedirect_RequiresHttpScheme() =>
        Assert.Throws<ArgumentException>(() => WebResult.ExternalRedirect(new Uri("javascript:alert(1)")));

    [Fact]
    public void Commands_RejectNonLocalNavigation()
    {
        Assert.Throws<ArgumentException>(() => WebCommand.Redirect("//evil.example"));
        Assert.Throws<ArgumentException>(() => WebCommand.Navigate("https://evil.example"));
    }

    [Fact]
    public void WithHeader_IsCaseInsensitive()
    {
        var result = WebResult.Text("x").WithHeader("X-Test", "1").WithHeader("x-test", "2");

        Assert.Single(result.Headers);
        Assert.Equal("2", result.Headers["X-TEST"]);
    }
}
