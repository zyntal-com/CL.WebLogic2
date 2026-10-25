using CL.WebLogic.ClientBundler.Output;

namespace CL.WebLogic.ClientBundler;

/// <summary>
/// Bundles the WebLogic client: <c>CL.WebLogic.ClientBundler --src &lt;dir&gt; --out &lt;dir&gt; [--version &lt;v&gt;]</c>.
/// Reads <c>bundle.json</c> in the source folder. Exit code 1 on any error (MSBuild shows the message).
/// </summary>
internal static class Program
{
    private static int Main(string[] args)
    {
        string? source = null, output = null, version = "0.0.0";
        for (var i = 0; i < args.Length - 1; i++)
        {
            switch (args[i])
            {
                case "--src": source = args[++i]; break;
                case "--out": output = args[++i]; break;
                case "--version": version = args[++i]; break;
            }
        }

        if (source is null || output is null)
        {
            Console.Error.WriteLine("usage: CL.WebLogic.ClientBundler --src <dir> --out <dir> [--version <v>]");
            return 2;
        }

        try
        {
            var written = new BundlerRun(source, output, version).Execute();
            Console.WriteLine($"WebLogic client: {written.Count} files written to {output}");
            return 0;
        }
        catch (Linking.BundleException ex)
        {
            Console.Error.WriteLine($"{ex.File}({ex.Line},1): error WLB001: {ex.Message}");
            return 1;
        }
    }
}
