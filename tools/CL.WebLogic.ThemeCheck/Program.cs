using CL.WebLogic.Theming.Services;

// Usage: dotnet run --project tools/CL.WebLogic.ThemeCheck -- <theme folder> [assets folder]
// Exit codes: 0 = every template parses, 1 = problems found, 2 = bad arguments.
if (args.Length is < 1 or > 2 || args[0] is "-h" or "--help")
{
    Console.Error.WriteLine("Usage: CL.WebLogic.ThemeCheck <theme folder> [assets folder (default: assets)]");
    return 2;
}

try
{
    var issues = WebThemeChecker.Check(args[0], args.Length > 1 ? args[1] : "assets");
    foreach (var issue in issues)
        Console.WriteLine(issue);
    Console.Error.WriteLine(issues.Count == 0 ? "All templates parse." : $"{issues.Count} template(s) need changes.");
    return issues.Count == 0 ? 0 : 1;
}
catch (DirectoryNotFoundException ex)
{
    Console.Error.WriteLine(ex.Message);
    return 2;
}
