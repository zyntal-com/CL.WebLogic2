using System.Diagnostics;
using CL.WebLogic.ClientBundler.Linking;

namespace CL.WebLogic.ClientBundler.Tests;

public sealed class BundleLinkerTests
{
    private static string Link(Dictionary<string, string> files, string entry = "main.js", BundleFormat format = BundleFormat.Iife) =>
        new BundleLinker(path => files.GetValueOrDefault(path)).Link(entry, format, "test");

    [Fact]
    public void Links_InDependencyOrder_WithRenamedImports()
    {
        var output = Link(new()
        {
            ["main.js"] = "import { add as plus } from './lib/math.js';\nimport * as text from './text.js';\nexport const result = text.shout(String(plus(1, 2)));",
            ["lib/math.js"] = "// adds\nexport function add(a, b) { return a + b; }",
            ["text.js"] = "import { add } from './lib/math.js';\nexport const shout = (s) => s + '!';\nconst hidden = add;\nexport { hidden as alias };"
        });

        Assert.True(output.IndexOf("__wl_m[\"lib/math.js\"] =", StringComparison.Ordinal) < output.IndexOf("__wl_m[\"text.js\"] =", StringComparison.Ordinal));
        Assert.Contains("const { add: plus } = __wl_m[\"lib/math.js\"];", output);
        Assert.Contains("const text = __wl_m[\"text.js\"];", output);
        Assert.Contains("return Object.freeze({ shout, alias: hidden });", output);
        Assert.DoesNotContain("// adds", output);
        Assert.DoesNotContain("export ", output.Replace("/*! test */", "", StringComparison.Ordinal));
    }

    [Fact]
    public void Esm_ReexportsTheEntry()
    {
        var output = Link(new() { ["main.js"] = "export const a = 1;\nexport function b() {}" }, format: BundleFormat.Esm);
        Assert.EndsWith("export const { a, b } = __wl_m[\"main.js\"];\n", output);
    }

    [Theory]
    [InlineData("export default 1;", "default exports")]
    [InlineData("export let x = 1;", "export let/var")]
    [InlineData("import x from './a.js';", "default imports")]
    [InlineData("import('./a.js');", "dynamic import")]
    [InlineData("const u = import.meta.url;", "import.meta")]
    [InlineData("import { a } from 'lodash';", "relative")]
    [InlineData("import { a } from '../../x.js';", "outside")]
    [InlineData("export * from './a.js';", "export *")]
    [InlineData("export { a } from './a.js';", "re-exports")]
    [InlineData("import { missing } from './a.js';", "not exported")]
    [InlineData("import { a } from './nope.js';", "not found")]
    public void UnsupportedForms_AreErrors(string source, string message)
    {
        var error = Assert.Throws<BundleException>(() => Link(new() { ["main.js"] = source, ["a.js"] = "export const a = 1;" }));
        Assert.Contains(message, error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("main.js", error.File);
    }

    [Fact]
    public void Cycles_AreErrors()
    {
        var error = Assert.Throws<BundleException>(() => Link(new()
        {
            ["main.js"] = "import { b } from './b.js';",
            ["b.js"] = "import { c } from './c.js'; export const b = 1;",
            ["c.js"] = "import { b } from './b.js'; export const c = 1;"
        }));
        Assert.Contains("b.js → c.js → b.js", error.Message);
    }

    [Fact]
    public void PropertyNamedImportOrExport_IsNotModuleSyntax()
    {
        var output = Link(new() { ["main.js"] = "const o = { import: 1, export: 2 };\nexport const v = o.import + o.export;" });
        Assert.Contains("o.import + o.export", output);
    }

    [Fact]
    public void Compact_KeepsLineBreaks_ForAsi()
    {
        var output = Link(new() { ["main.js"] = "let a = 1 /* x */\nlet b = a\n\n\n   ;[a, b] = [b, a]\nexport const r = a;" });
        Assert.Contains("let a = 1\nlet b = a\n;[a, b] = [b, a]", output);
    }

    [Fact]
    public void RealClient_Bundles_AndRunsInNode()
    {
        var source = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../src/CL.WebLogic/Client/src"));
        var output = Path.Combine(Path.GetTempPath(), "wl-bundle-" + Guid.NewGuid().ToString("N"));
        try
        {
            var written = new Output.BundlerRun(source, output, "1.2.3").Execute();
            Assert.Contains("weblogic.js", written);
            Assert.Contains("weblogic.esm.js", written);
            Assert.True(File.Exists(Path.Combine(output, "weblogic.js.br")));

            if (!TryRun("node", "--version", out _))
                return; // Node is optional for this test; CI has it.
            Assert.True(TryRun("node", $"-e \"require('{Path.Combine(output, "weblogic.js").Replace('\\', '/')}')\"", out var error), error);
            foreach (var esm in new[] { "weblogic.esm.js", "weblogic.bootstrap5.esm.js", "weblogic.tests.esm.js" })
            {
                var copy = Path.Combine(output, esm + ".mjs");
                File.Copy(Path.Combine(output, esm), copy);
                Assert.True(TryRun("node", $"--check \"{copy}\"", out error), esm + ": " + error);
            }
        }
        finally
        {
            if (Directory.Exists(output))
                Directory.Delete(output, recursive: true);
        }
    }

    private static bool TryRun(string file, string arguments, out string error)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo(file, arguments) { RedirectStandardError = true, RedirectStandardOutput = true, UseShellExecute = false })!;
            error = process.StandardError.ReadToEnd();
            process.WaitForExit(30000);
            return process.ExitCode == 0;
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }
}
