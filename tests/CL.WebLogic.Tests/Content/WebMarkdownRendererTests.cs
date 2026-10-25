using CL.WebLogic.Content.Models;
using CL.WebLogic.Content.Services;

namespace CL.WebLogic.Tests.Content;

public sealed class WebMarkdownRendererTests
{
    private static string Md(string markdown, WebMarkdownOptions? options = null) =>
        new WebMarkdownRenderer(options).Render(markdown).Html;

    [Theory]
    [InlineData("# Title", "<h1>Title</h1>")]
    [InlineData("### Sub ###", "<h3>Sub</h3>")]
    [InlineData("#NoSpace", "<p>#NoSpace</p>")]
    [InlineData("a *b* **c** ***d*** ~~e~~ `f`", "<p>a <em>b</em> <strong>c</strong> <em><strong>d</strong></em> <del>e</del> <code>f</code></p>")]
    [InlineData("snake_case_name and _em_", "<p>snake_case_name and <em>em</em></p>")]
    [InlineData("\\*not em\\*", "<p>*not em*</p>")]
    [InlineData("line one  \nline two", "<p>line one<br>\nline two</p>")]
    [InlineData("---", "<hr>")]
    [InlineData("&copy; &amp; &bogus;", "<p>© &amp; &amp;bogus;</p>")]
    public void Basics(string input, string expected) => Assert.Equal(expected, Md(input));

    [Theory]
    [InlineData("[", 1)]
    [InlineData("*x ", 3)]
    [InlineData("`a ``", 5)]
    [InlineData("[a](<x", 6)]
    [InlineData("&", 1)]
    public void Pathological_input_renders_in_linear_time(string unit, int length)
    {
        // Regression (security review): unbounded delimiter searches made these quadratic (~3.5 s for 160 KB).
        // Compares scaling instead of wall-clock time, so a busy CI runner cannot fail it: 4x the input
        // costs ~4x when linear and ~16x when quadratic.
        var small = BestOf(string.Concat(Enumerable.Repeat(unit, 40_000 / length)));
        var large = BestOf(string.Concat(Enumerable.Repeat(unit, 160_000 / length)));
        Assert.True(large < small * 8 + 20, $"40 KB: {small:F1} ms, 160 KB: {large:F1} ms");
    }

    private static double BestOf(string input)
    {
        Md(input); // warm-up
        var best = double.MaxValue;
        for (var i = 0; i < 3; i++)
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            Md(input);
            best = Math.Min(best, watch.Elapsed.TotalMilliseconds);
        }
        return best;
    }

    [Fact]
    public void Links_ImagesAndAutolinks()
    {
        Assert.Equal("<p><a href=\"https://x.test/a(b)\" title=\"T\">x <em>y</em></a></p>", Md("[x *y*](https://x.test/a(b) \"T\")"));
        Assert.Equal("<p><img src=\"/i.png\" alt=\"alt text\"></p>", Md("![alt *text*](/i.png)"));
        Assert.Equal("<p><a href=\"https://x.test\">https://x.test</a> and <a href=\"mailto:a@b.test\">a@b.test</a></p>", Md("<https://x.test> and <a@b.test>"));
        Assert.Equal("<p>see <a href=\"https://x.test/p\">https://x.test/p</a>.</p>", Md("see https://x.test/p."));
        Assert.Equal("<p><a href=\"https://www.x.test\">www.x.test</a></p>", Md("www.x.test"));
    }

    [Theory]
    [InlineData("[x](javascript:alert(1))", "<p>x</p>")]
    [InlineData("[x](JAVASCRIPT:alert(1))", "<p>x</p>")]
    [InlineData("[x](data:text/html,<script>alert(1)</script>)", "<p>x</p>")]
    [InlineData("![x](javascript:alert(1))", "<p>x</p>")]
    [InlineData("<javascript:alert(1)>", "<p>&lt;javascript:alert(1)&gt;</p>")]
    [InlineData("<script>alert(1)</script>", "<p>&lt;script&gt;alert(1)&lt;/script&gt;</p>")]
    [InlineData("<img src=x onerror=alert(1)>", "<p>&lt;img src=x onerror=alert(1)&gt;</p>")]
    [InlineData("[x](\"onmouseover=alert(1))", "<p><a href=\"&quot;onmouseover=alert(1)\">x</a></p>")]
    [InlineData("[a](https://x.test \"t\\\" onclick=\\\"alert(1)\")", "<p><a href=\"https://x.test\" title=\"t&quot; onclick=&quot;alert(1)\">a</a></p>")]
    [InlineData("`<script>`", "<p><code>&lt;script&gt;</code></p>")]
    public void Xss_IsNeutralized(string input, string expected) => Assert.Equal(expected, Md(input));

    [Fact]
    public void FencedAndIndentedCode()
    {
        Assert.Equal("<pre><code class=\"language-cs\">var x = &quot;&lt;b&gt;&quot;;\n</code></pre>", Md("```cs\nvar x = \"<b>\";\n```"));
        Assert.Equal("<pre><code>code line\n</code></pre>", Md("    code line"));
    }

    [Fact]
    public void Lists_TightLooseNestedOrdered()
    {
        Assert.Equal("<ul>\n<li>a</li>\n<li>b\n<ul>\n<li>c</li>\n</ul></li>\n</ul>", Md("- a\n- b\n  - c"));
        Assert.Equal("<ol start=\"3\">\n<li>x</li>\n<li>y</li>\n</ol>", Md("3. x\n4. y"));
        Assert.Equal("<ul>\n<li><p>a</p></li>\n<li><p>b</p></li>\n</ul>", Md("- a\n\n- b"));
    }

    [Fact]
    public void Blockquote_AndTable()
    {
        Assert.Equal("<blockquote>\n<p>quoted\nlazy</p>\n</blockquote>", Md("> quoted\nlazy"));
        Assert.Equal(
            "<table>\n<thead>\n<tr><th>A</th><th align=\"right\">B</th></tr>\n</thead>\n<tbody>\n<tr><td><em>1</em></td><td align=\"right\">2</td></tr>\n</tbody>\n</table>",
            Md("| A | B |\n|---|--:|\n| *1* | 2 |"));
    }

    [Fact]
    public void UserContentOptions_AddRel_AndHeadingIds()
    {
        Assert.Equal("<p><a href=\"https://x.test\" rel=\"nofollow ugc noopener noreferrer\">x</a></p>", Md("[x](https://x.test)", WebMarkdownOptions.UserContent));
        Assert.Equal("<h2 id=\"h-hello-world\">Hello World</h2>", Md("## Hello World", new WebMarkdownOptions { GenerateHeadingIds = true }));
    }

    [Fact]
    public void PathologicalInput_StaysFast()
    {
        var input = string.Concat(Enumerable.Repeat("*a _b [c](", 2000)) + string.Concat(Enumerable.Repeat("> ", 500)) + "x";
        var watch = System.Diagnostics.Stopwatch.StartNew();
        Md(input);
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(5), $"took {watch.Elapsed}");
    }
}
