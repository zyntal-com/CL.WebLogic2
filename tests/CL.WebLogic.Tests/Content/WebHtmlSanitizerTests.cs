using CL.WebLogic.Content.Services;

namespace CL.WebLogic.Tests.Content;

public sealed class WebHtmlSanitizerTests
{
    private static readonly WebHtmlSanitizer Sanitizer = new();

    private static string Clean(string html) => Sanitizer.Sanitize(html).Html;

    /// <summary>Classic XSS vectors: none may produce an executable construct.</summary>
    public static TheoryData<string> XssCorpus =>
    [
        "<script>alert(1)</script>",
        "<SCRIPT SRC=//evil/x.js></SCRIPT>",
        "<img src=x onerror=alert(1)>",
        "<img src=\"javascript:alert(1)\">",
        "<a href=\"javascript:alert(1)\">x</a>",
        "<a href=\"jav&#x09;ascript:alert(1)\">x</a>",
        "<a href=\"&#106;&#97;&#118;&#97;&#115;&#99;&#114;&#105;&#112;&#116;&#58;alert(1)\">x</a>",
        "<a href=\" javascript:alert(1)\">x</a>",
        "<a href=\"JaVaScRiPt:alert(1)\">x</a>",
        "<a href=\"vbscript:msgbox(1)\">x</a>",
        "<a href=\"data:text/html;base64,PHNjcmlwdD5hbGVydCgxKTwvc2NyaXB0Pg==\">x</a>",
        "<svg onload=alert(1)>",
        "<svg><script>alert(1)</script></svg>",
        "<math><mi xlink:href=\"javascript:alert(1)\">x</mi></math>",
        "<iframe src=\"javascript:alert(1)\"></iframe>",
        "<object data=\"x.swf\"></object>",
        "<embed src=\"x.swf\">",
        "<body onload=alert(1)>",
        "<div style=\"background:url(javascript:alert(1))\">x</div>",
        "<style>@import 'evil.css';</style>",
        "<p onmouseover=\"alert(1)\">x</p>",
        "<form action=\"javascript:alert(1)\"><button>x</button></form>",
        "<input autofocus onfocus=alert(1)>",
        "<details open ontoggle=alert(1)>",
        "<a href=\"#\" onclick=\"alert(1)\">x</a>",
        "<img src=\"x\" =\"\" onerror=\"alert(1)\">",
        "<img/src=x/onerror=alert(1)>",
        "<<script>script>alert(1)<</script>/script>",
        "<scr<script>ipt>alert(1)</script>",
        "<!--<script>alert(1)--><script>alert(1)</script>",
        "<noscript><p title=\"</noscript><img src=x onerror=alert(1)>\"></noscript>",
        "<textarea><script>alert(1)</script></textarea>",
        "<template><script>alert(1)</script></template>",
        "<a href=\"\u0001javascript:alert(1)\">x</a>",
        "<base href=\"javascript:alert(1)//\">",
        "<meta http-equiv=\"refresh\" content=\"0;url=javascript:alert(1)\">",
        "<link rel=stylesheet href=evil.css>",
        "<video><source onerror=\"alert(1)\"></video>",
        "<p id=\"__proto__\" name=\"location\">x</p>",
    ];

    [Theory, MemberData(nameof(XssCorpus))]
    public void XssVectors_AreNeutralized(string input)
    {
        var output = Clean(input);

        Assert.DoesNotContain("<script", output, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("javascript:", output, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("vbscript:", output, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("data:text", output, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotMatch(@"<[^>]*\son\w+\s*=", output);
        Assert.DoesNotContain("<svg", output, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<iframe", output, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<style", output, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("style=", output, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(" id=", output, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<base", output, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<meta", output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void KeepsFormatting_AndAddsLinkRel()
    {
        var output = Clean("<p>Hi <strong>there</strong> <a href=\"https://x.test/a?b=1&amp;c=2\" target=\"_blank\">link</a></p>");

        Assert.Equal("<p>Hi <strong>there</strong> <a href=\"https://x.test/a?b=1&amp;c=2\" target=\"_blank\" rel=\"nofollow ugc noopener noreferrer\">link</a></p>", output);
    }

    [Fact]
    public void UnknownElements_AreUnwrapped_DroppedElementsLoseContent()
    {
        Assert.Equal("<p>keep text</p>", Clean("<p><custom-el>keep</custom-el> text<script>gone()</script></p>"));
    }

    [Fact]
    public void OutputIsBalanced()
    {
        Assert.Equal("<div><p><em>x</em></p></div>", Clean("<div><p><em>x"));
        Assert.Equal("<p>a</p>b", Clean("<p>a</div></p>b</p>"));
    }

    [Fact]
    public void AttributeValues_AreReEncoded()
    {
        Assert.Equal("<abbr title=\"&quot;&gt;&lt;script&gt;\">x</abbr>", Clean("<abbr title='\"><script>'>x</abbr>"));
    }

    [Fact]
    public void Text_IsEncoded() =>
        Assert.Equal("1 &lt; 2 &amp;&amp; 3 &gt; 2", Clean("1 &lt; 2 && 3 > 2"));

    [Theory]
    [InlineData("<img src=\"x\" width=\"100\" height=\"9999999\">", "<img src=\"x\" width=\"100\">")]
    [InlineData("<td colspan=\"2\" rowspan=\"x\">c</td>", "<td colspan=\"2\">c</td>")]
    [InlineData("<a href=\"/local\" target=\"_top\">x</a>", "<a href=\"/local\" rel=\"nofollow ugc noopener noreferrer\">x</a>")]
    public void AttributeValues_AreValidated(string input, string expected) => Assert.Equal(expected, Clean(input));
}
