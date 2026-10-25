namespace MiniBlog.Application;

/// <summary>
/// Copies the plugins shipped with the app (read-only in the image) into the writable plugins
/// folder on the data volume. Assemblies are refreshed on every start; config and logs CodeLogic
/// keeps beside them are left alone.
/// </summary>
public static class PluginInstaller
{
    /// <summary>Installs from <paramref name="source"/> into <paramref name="target"/>; returns the target.</summary>
    public static string Install(string source, string target)
    {
        if (!Directory.Exists(source))
            return target;
        foreach (var plugin in Directory.GetDirectories(source, "*.Plugin"))
        {
            var destination = Path.Combine(target, Path.GetFileName(plugin));
            Directory.CreateDirectory(destination);
            foreach (var file in Directory.GetFiles(plugin).Where(f => f.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".pdb", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".deps.json", StringComparison.OrdinalIgnoreCase)))
                File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), overwrite: true);
        }
        return target;
    }
}
