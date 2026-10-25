namespace Samples.Hosting;

/// <summary>
/// Where a sample keeps its state. Everything writable lives under one data folder
/// (<c>DEMO_DATA_DIR</c>, default <c>data</c>) so a container mounts a single volume.
/// </summary>
public sealed class SamplePaths
{
    private SamplePaths(string dataRoot) => DataRoot = Path.GetFullPath(dataRoot);

    /// <summary>The data folder.</summary>
    public string DataRoot { get; }

    /// <summary>CodeLogic's framework root (framework and library config, logs).</summary>
    public string FrameworkRoot => Path.Combine(DataRoot, "codelogic");

    /// <summary>CodeLogic's application root (application config, logs).</summary>
    public string ApplicationRoot => Path.Combine(DataRoot, "app");

    /// <summary>The ASP.NET DataProtection key ring. Losing it signs everyone out and voids 2FA secrets.</summary>
    public string KeysFolder => Path.Combine(DataRoot, "keys");

    /// <summary>The paths for <c>DEMO_DATA_DIR</c> (or <c>data</c>).</summary>
    public static SamplePaths FromEnvironment() => new(DemoEnvironment.Text("DEMO_DATA_DIR") ?? "data");

    /// <summary>A folder under the data root, created when missing.</summary>
    public string Folder(string name)
    {
        var path = Path.Combine(DataRoot, name);
        Directory.CreateDirectory(path);
        return path;
    }

    /// <summary>The config file of a CodeLogic library section, e.g. (<c>CL.WebLogic</c>, <c>weblogic</c>).</summary>
    public string LibraryConfig(string libraryId, string section) =>
        Path.Combine(FrameworkRoot, "Libraries", libraryId, $"config.{section}.json");

    /// <summary>The application's config file.</summary>
    public string ApplicationConfig(string? section = null) =>
        Path.Combine(ApplicationRoot, section is null ? "config.json" : $"config.{section}.json");
}
