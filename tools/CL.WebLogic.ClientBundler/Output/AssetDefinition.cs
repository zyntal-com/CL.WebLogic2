namespace CL.WebLogic.ClientBundler.Output;

/// <summary>One entry of <c>bundle.json</c> → <c>assets</c> (copied with comments stripped).</summary>
internal sealed class AssetDefinition
{
    /// <summary>Output name.</summary>
    public string Name { get; set; } = "";

    /// <summary>Source file (root-relative).</summary>
    public string Source { get; set; } = "";
}
