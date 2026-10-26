using CL.WebLogic.Theming.Services;

namespace CL.WebLogic.Tests.Theming;

public sealed class WebThemeCheckerTests : IDisposable
{
    private readonly string _theme = Directory.CreateTempSubdirectory("wl-themecheck-").FullName;

    [Fact]
    public void Reports_each_broken_template_with_its_position_and_skips_assets()
    {
        Write("templates/ok.html", "<p>{model:name}</p>");
        Write("templates/handler.html", "<p>fine</p>\n<button onclick=\"go({model:id})\">Go</button>");
        Write("partials/script.html", "<script>var user = {model:name};</script>");
        Write("layouts/broken.html", "{if:model:x}<p>never closed</p>");
        Write("assets/docs/readme.html", "<a onclick=\"x()\">not a template</a>");
        Write(".git/hooks/sample.html", "{if:");

        var issues = WebThemeChecker.Check(_theme);

        Assert.Equal(["layouts/broken.html", "partials/script.html", "templates/handler.html"], issues.Select(i => i.File));
        var handler = issues.Single(i => i.File == "templates/handler.html");
        Assert.Equal(2, handler.Line);
        Assert.True(handler.Column > 1);
        Assert.StartsWith("templates/handler.html(2,", handler.ToString());
        Assert.DoesNotContain("templates/handler.html(2,", handler.Message); // the position is not repeated
    }

    [Theory]
    [InlineData("samples/StarterWebsite/theme")]
    [InlineData("samples/MiniBlog/theme")]
    public void The_sample_themes_parse(string theme)
    {
        // Walk up to the repository root (absent when the tests run from a package-only checkout).
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "CL.WebLogic2.slnx")))
            {
                Assert.Empty(WebThemeChecker.Check(Path.Combine(dir.FullName, theme)));
                return;
            }
        }
    }

    [Fact]
    public void A_missing_folder_is_an_error() =>
        Assert.Throws<DirectoryNotFoundException>(() => WebThemeChecker.Check(Path.Combine(_theme, "nope")));

    public void Dispose() => Directory.Delete(_theme, recursive: true);

    private void Write(string path, string content)
    {
        var full = Path.Combine(_theme, path);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, content);
    }
}
