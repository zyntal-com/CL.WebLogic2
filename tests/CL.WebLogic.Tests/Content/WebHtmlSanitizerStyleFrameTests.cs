using CL.WebLogic.Content.Models;
using CL.WebLogic.Content.Services;

namespace CL.WebLogic.Tests.Content;

/// <summary>Opt-in <c>style</c> declarations and embed iframes.</summary>
public sealed class WebHtmlSanitizerStyleFrameTests
{
    private static readonly WebHtmlSanitizer Sanitizer = new(new WebHtmlSanitizerOptions
    {
        AllowedStyleProperties = new Dictionary<string, Func<string, bool>>(StringComparer.OrdinalIgnoreCase)
        {
            ["width"] = WebCssValues.Length,
            ["color"] = WebCssValues.Color,
            ["background-color"] = WebCssValues.Color,
            ["text-align"] = WebCssValues.OneOf("left", "center", "right", "justify")
        },
        AllowedFrameSources = [new WebFrameSource("www.youtube-nocookie.com", "/embed/"), new WebFrameSource("player.twitch.tv")]
    });

    private static string Clean(string html) => Sanitizer.Sanitize(html).Html;

    [Theory, MemberData(nameof(WebHtmlSanitizerTests.XssCorpus), MemberType = typeof(WebHtmlSanitizerTests))]
    public void XssVectors_StayNeutralized_WithStylesAndFramesOn(string input)
    {
        var output = Clean(input);

        Assert.DoesNotContain("<script", output, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("javascript:", output, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("url(", output, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotMatch(@"<[^>]*\son\w+\s*=", output);
        Assert.DoesNotContain("<iframe", output, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("<img src=\"/m/1\" style=\"width:50%\">", "<img src=\"/m/1\" style=\"width: 50%\">")]
    [InlineData("<span style=\"color: #ff0000; font-family: Comic Sans\">x</span>", "<span style=\"color: #ff0000\">x</span>")]
    [InlineData("<p style=\"text-align:center;color:rgb(10, 20, 30)\">x</p>", "<p style=\"text-align: center; color: rgb(10, 20, 30)\">x</p>")]
    [InlineData("<p style=\"color: red !important\">x</p>", "<p style=\"color: red\">x</p>")]
    public void AllowedDeclarations_AreKept_AndRebuilt(string input, string expected) => Assert.Equal(expected, Clean(input));

    [Theory]
    [InlineData("<p style=\"background-color: url(https://evil.test/x.png)\">x</p>")]
    [InlineData("<p style=\"color: expression(alert(1))\">x</p>")]
    [InlineData("<p style=\"color: red; behavior: url(x.htc)\">x</p>", "color: red")]
    [InlineData("<p style=\"width: calc(100% - 1px)\">x</p>")]
    [InlineData("<p style=\"color: var(--x)\">x</p>")]
    [InlineData("<p style=\"color: r\\65 d\">x</p>")]
    [InlineData("<p style=\"color: red/**/\">x</p>")]
    [InlineData("<p style=\"color: &quot;red&quot;\">x</p>")]
    [InlineData("<p style=\"width: 500%\">x</p>")]
    [InlineData("<p style=\"position: fixed\">x</p>")]
    public void DangerousOrUnknownDeclarations_AreDropped(string input, string? kept = null)
    {
        var output = Clean(input);
        if (kept is null)
            Assert.Equal("<p>x</p>", output);
        else
            Assert.Equal($"<p style=\"{kept}\">x</p>", output);
    }

    [Fact]
    public void AllowedFrame_IsRebuiltWithSandbox()
    {
        var output = Clean("<iframe src=\"https://www.youtube-nocookie.com/embed/abc?rel=0\" width=\"560\" height=\"315\" onload=\"x()\" srcdoc=\"<b>\" allow=\"camera\">fallback</iframe><p>after</p>");

        Assert.Equal(
            "<iframe src=\"https://www.youtube-nocookie.com/embed/abc?rel=0\" width=\"560\" height=\"315\" sandbox=\"allow-scripts allow-same-origin allow-presentation allow-popups allow-popups-to-escape-sandbox\" allow=\"accelerometer; encrypted-media; gyroscope; picture-in-picture; fullscreen\" allowfullscreen referrerpolicy=\"strict-origin-when-cross-origin\"></iframe><p>after</p>",
            output);
    }

    [Theory]
    [InlineData("<iframe src=\"http://www.youtube-nocookie.com/embed/abc\"></iframe>")]
    [InlineData("<iframe src=\"https://www.youtube-nocookie.com/watch?v=abc\"></iframe>")]
    [InlineData("<iframe src=\"https://www.youtube-nocookie.com.evil.test/embed/abc\"></iframe>")]
    [InlineData("<iframe src=\"https://user@www.youtube-nocookie.com/embed/abc\"></iframe>")]
    [InlineData("<iframe src=\"https://www.youtube-nocookie.com:8443/embed/abc\"></iframe>")]
    [InlineData("<iframe src=\"https://evil.test/embed/abc\"><p>gone</p></iframe>")]
    public void OtherFrames_AreDropped(string input) => Assert.Equal("<p>ok</p>", Clean(input + "<p>ok</p>"));

    [Fact]
    public void Defaults_StillDropStylesAndFrames() =>
        Assert.Equal("<p>x</p>", new WebHtmlSanitizer().Sanitize("<p style=\"color:red\">x</p><iframe src=\"https://www.youtube-nocookie.com/embed/a\"></iframe>").Html);

    [Theory]
    [InlineData("12px", true)]
    [InlineData("0", true)]
    [InlineData("100%", true)]
    [InlineData("101%", false)]
    [InlineData("-5px", false)]
    [InlineData("12", false)]
    public void Length_Check(string value, bool expected) => Assert.Equal(expected, WebCssValues.Length(value));

    [Theory]
    [InlineData("#abc", true)]
    [InlineData("#11223344", true)]
    [InlineData("rgba(1, 2, 3, 0.5)", true)]
    [InlineData("hsl(120 50% 50%)", true)]
    [InlineData("rebeccapurple", true)]
    [InlineData("#ggg", false)]
    [InlineData("rgb(1,2)", false)]
    public void Color_Check(string value, bool expected) => Assert.Equal(expected, WebCssValues.Color(value));
}
