using System.Runtime.CompilerServices;
using CL.WebLogic.Templates.Rendering;
using CL.WebLogic.Testing.Services;

namespace CL.WebLogic.Tests.Templates;

/// <summary>
/// Golden snapshots: each template under <c>Golden/theme/templates</c> is rendered by both engines
/// and compared with the committed <c>Golden/expected/{name}.html</c>. On a mismatch the actual
/// output is written next to the snapshot as <c>{name}.actual.html</c> for diffing; set
/// <c>WEBLOGIC_UPDATE_GOLDEN=1</c> to accept new output deliberately.
/// </summary>
public sealed class GoldenTemplateTests
{
    public static TheoryData<string, string> Cases()
    {
        var data = new TheoryData<string, string>();
        foreach (var file in Directory.EnumerateFiles(Path.Combine(AppContext.BaseDirectory, "Templates", "Golden", "theme", "templates"), "*.html"))
        {
            var name = Path.GetFileNameWithoutExtension(file);
            data.Add(name, "closure");
            data.Add(name, "interpreter");
        }
        return data;
    }

    [Theory, MemberData(nameof(Cases))]
    public async Task MatchesSnapshot(string name, string engine)
    {
        var renderer = new TemplateTestRenderer(engine == "closure" ? new ClosureTemplateCompiler() : new TemplateInterpreter());
        var themeRoot = Path.Combine(AppContext.BaseDirectory, "Templates", "Golden", "theme");
        foreach (var file in Directory.EnumerateFiles(themeRoot, "*.html", SearchOption.AllDirectories))
            renderer.Templates.Add(Path.GetRelativePath(themeRoot, file).Replace(Path.DirectorySeparatorChar, '/'), await File.ReadAllTextAsync(file));

        var actual = (await renderer.RenderAsync("templates/" + name, GoldenModel())).Replace("\r\n", "\n", StringComparison.Ordinal);

        var expectedPath = Path.Combine(SourceDirectory(), "Golden", "expected", name + ".html");
        if (Environment.GetEnvironmentVariable("WEBLOGIC_UPDATE_GOLDEN") == "1")
        {
            Directory.CreateDirectory(Path.GetDirectoryName(expectedPath)!);
            await File.WriteAllTextAsync(expectedPath, actual);
        }

        var expected = File.Exists(expectedPath)
            ? (await File.ReadAllTextAsync(expectedPath)).Replace("\r\n", "\n", StringComparison.Ordinal)
            : null;
        if (expected != actual)
            await File.WriteAllTextAsync(Path.ChangeExtension(expectedPath, ".actual.html"), actual);
        Assert.Equal(expected, actual);
    }

    private static Dictionary<string, object?> GoldenModel() => new(StringComparer.OrdinalIgnoreCase)
    {
        ["title"] = "<Hello & 'World'>",
        ["count"] = 3,
        ["kind"] = "a",
        ["n"] = 1234.5,
        ["x"] = "live",
        ["html"] = "<b onclick=\"steal()\">bold</b><script>alert(1)</script><a href=\"javascript:x\">j</a>",
        ["url"] = "https://example.test/a?b=1&c=2",
        ["bad_url"] = " JavaScript:alert(1)",
        ["file"] = "my file.png",
        ["color"] = "red;background:url(x)",
        ["body"] = "line 1\n<line 2>",
        ["md"] = "## Heading\n\nSome *markdown* with [a link](https://example.test) and <b>html</b>.",
        ["rows"] = new[]
        {
            new Dictionary<string, object?> { ["name"] = "a", ["tags"] = new[] { new Dictionary<string, object?> { ["t"] = "x" } } },
            new Dictionary<string, object?> { ["name"] = "b<", ["tags"] = Array.Empty<object>() }
        }
    };

    private static string SourceDirectory([CallerFilePath] string path = "") => Path.GetDirectoryName(path)!;
}
