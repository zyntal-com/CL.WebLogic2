namespace CL.WebLogic.ClientBundler.Output;

/// <summary>The <c>bundle.json</c> file.</summary>
internal sealed class BundleConfig
{
    /// <summary>Banner comment (<c>{version}</c> is replaced).</summary>
    public string Header { get; set; } = "";

    /// <summary>Script bundles.</summary>
    public List<BundleDefinition> Bundles { get; set; } = [];

    /// <summary>Other assets (CSS).</summary>
    public List<AssetDefinition> Assets { get; set; } = [];
}
