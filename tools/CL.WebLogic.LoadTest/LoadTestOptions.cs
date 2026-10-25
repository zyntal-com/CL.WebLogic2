using System.Globalization;

namespace CL.WebLogic.LoadTest;

/// <summary>Command line: <c>CL.WebLogic.LoadTest [--duration 10] [--concurrency 32] [--warmup 2] [--header "Name: value"] url...</c>.</summary>
internal sealed record LoadTestOptions(IReadOnlyList<Uri> Urls, TimeSpan Duration, int Concurrency, TimeSpan Warmup, IReadOnlyList<KeyValuePair<string, string>> Headers)
{
    public static LoadTestOptions Parse(string[] args)
    {
        var urls = new List<Uri>();
        var duration = 10d;
        var warmup = 2d;
        var concurrency = 32;
        var headers = new List<KeyValuePair<string, string>>();
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--duration": duration = double.Parse(args[++i], CultureInfo.InvariantCulture); break;
                case "--warmup": warmup = double.Parse(args[++i], CultureInfo.InvariantCulture); break;
                case "--concurrency": concurrency = int.Parse(args[++i], CultureInfo.InvariantCulture); break;
                case "--header":
                    var header = args[++i];
                    var colon = header.IndexOf(':', StringComparison.Ordinal);
                    headers.Add(new(header[..colon].Trim(), header[(colon + 1)..].Trim()));
                    break;
                default: urls.Add(new Uri(args[i], UriKind.Absolute)); break;
            }
        }
        if (urls.Count == 0)
            throw new ArgumentException("Give at least one URL.");
        return new LoadTestOptions(urls, TimeSpan.FromSeconds(duration), Math.Max(1, concurrency), TimeSpan.FromSeconds(warmup), headers);
    }
}
