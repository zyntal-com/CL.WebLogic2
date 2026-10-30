namespace CL.WebLogic.Theming.Services;

/// <summary>
/// Symbolic link checks for theme files. <see cref="Path.GetFullPath(string)"/> does not resolve
/// links, so a link anywhere below the theme root (a Git theme can carry them) could point outside
/// it. The root itself is the operator's choice and may be a link.
/// </summary>
internal static class ThemeLinks
{
    private static readonly StringComparison PathComparison =
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    /// <summary>True when <paramref name="file"/> or any folder between it and <paramref name="root"/> is a link.</summary>
    public static bool AnyBelow(string root, FileInfo file)
    {
        if (file.LinkTarget is not null)
            return true;
        var top = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        for (var folder = file.Directory; folder is not null; folder = folder.Parent)
        {
            if (string.Equals(Path.TrimEndingDirectorySeparator(folder.FullName), top, PathComparison))
                return false;
            if (folder.LinkTarget is not null)
                return true;
        }
        return true; // not under the root at all
    }
}
