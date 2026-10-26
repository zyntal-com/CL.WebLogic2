using System.Text;
using CL.WebLogic.Templates.Parsing;
using CL.WebLogic.Theming.Models;

namespace CL.WebLogic.Theming.Services;

/// <summary>
/// Parses every template of a theme folder and reports the ones the engine would refuse (unsafe
/// constructs such as inline <c>on*</c> handlers, unquoted attribute values, script values without
/// <c>|json</c>, and syntax errors), with file, line and column. Use it before switching a theme,
/// in a test (<c>Assert.Empty(WebThemeChecker.Check("theme"))</c>) or with the
/// <c>tools/CL.WebLogic.ThemeCheck</c> command. Reports the first problem of each file.
/// </summary>
public static class WebThemeChecker
{
    /// <summary>
    /// Checks every <c>.html</c> file under <paramref name="themeFolder"/>, skipping the assets
    /// folder (<paramref name="assetsFolder"/>, served as files, not templates) and dot-folders.
    /// </summary>
    public static IReadOnlyList<WebThemeIssue> Check(string themeFolder, string assetsFolder = "assets")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(themeFolder);
        var root = Path.GetFullPath(themeFolder);
        if (!Directory.Exists(root))
            throw new DirectoryNotFoundException($"Theme folder '{themeFolder}' does not exist.");

        var assets = Path.GetFullPath(Path.Combine(root, assetsFolder)) + Path.DirectorySeparatorChar;
        var issues = new List<WebThemeIssue>();
        foreach (var file in Directory.EnumerateFiles(root, "*.html", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
        {
            var relative = Path.GetRelativePath(root, file).Replace('\\', '/');
            if (file.StartsWith(assets, StringComparison.OrdinalIgnoreCase) || relative.Split('/').Any(s => s.StartsWith('.')))
                continue;
            if (CheckSource(File.ReadAllText(file, Encoding.UTF8), relative) is { } issue)
                issues.Add(issue);
        }
        return issues;
    }

    /// <summary>Checks one template source; null when it parses.</summary>
    public static WebThemeIssue? CheckSource(string source, string path)
    {
        ArgumentNullException.ThrowIfNull(source);
        try
        {
            TemplateParser.Parse(source, path);
            return null;
        }
        catch (TemplateParseException ex)
        {
            var message = ex.Message;
            var prefix = $"{path}({ex.Line},{ex.Column}): ";
            if (message.StartsWith(prefix, StringComparison.Ordinal))
                message = message[prefix.Length..];
            return new WebThemeIssue(path, ex.Line, ex.Column, message);
        }
    }
}
