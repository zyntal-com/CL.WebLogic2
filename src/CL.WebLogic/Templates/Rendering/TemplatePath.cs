namespace CL.WebLogic.Templates.Rendering;

/// <summary>Normalizes template references to theme-relative paths that cannot leave the theme.</summary>
public static class TemplatePath
{
    private static readonly string[] RootFolders = ["templates/", "layouts/", "partials/", "widgets/", "errors/", "forms/", "emails/"];

    /// <summary>
    /// Normalizes <paramref name="path"/> as written in <paramref name="currentTemplatePath"/>:
    /// adds <c>.html</c>, keeps theme-root folders as-is, resolves relative paths against the
    /// current template's folder. Throws when the result would leave the theme.
    /// </summary>
    public static string Normalize(string path, string? currentTemplatePath = null)
    {
        var trimmed = (path ?? string.Empty).Trim().Replace('\\', '/');
        if (trimmed.Length == 0)
            throw new TemplateRenderException("Empty template path.", currentTemplatePath);

        if (!trimmed.EndsWith(".html", StringComparison.OrdinalIgnoreCase))
            trimmed += ".html";

        List<string> parts;
        if (trimmed.StartsWith('/'))
            parts = [];
        else if (RootFolders.Any(f => trimmed.StartsWith(f, StringComparison.OrdinalIgnoreCase)) || string.IsNullOrEmpty(currentTemplatePath))
            parts = [];
        else
            parts = currentTemplatePath.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries).SkipLast(1).ToList();

        foreach (var part in trimmed.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (part == ".")
                continue;
            if (part == "..")
            {
                if (parts.Count == 0)
                    throw new TemplateRenderException($"Template path '{path}' leaves the theme.", currentTemplatePath);
                parts.RemoveAt(parts.Count - 1);
                continue;
            }
            if (part.Contains(':') || part.StartsWith('.'))
                throw new TemplateRenderException($"Template path '{path}' is not allowed.", currentTemplatePath);
            parts.Add(part);
        }

        return string.Join('/', parts);
    }
}
