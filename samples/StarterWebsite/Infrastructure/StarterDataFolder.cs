namespace StarterWebsite.Infrastructure;

/// <summary>The folder the starter's JSON stores write to (under the data volume).</summary>
public sealed class StarterDataFolder
{
    /// <summary>Creates the folder when missing.</summary>
    public StarterDataFolder(string path)
    {
        Path = path;
        Directory.CreateDirectory(path);
    }

    /// <summary>The folder.</summary>
    public string Path { get; }

    /// <summary>A file in the folder.</summary>
    public string File(string name) => System.IO.Path.Combine(Path, name);
}
