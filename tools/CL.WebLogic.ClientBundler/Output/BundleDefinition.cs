namespace CL.WebLogic.ClientBundler.Output;

/// <summary>One entry of <c>bundle.json</c> → <c>bundles</c>.</summary>
internal sealed class BundleDefinition
{
    /// <summary>Output base name, e.g. <c>weblogic</c> → <c>weblogic.js</c> / <c>weblogic.esm.js</c>.</summary>
    public string Name { get; set; } = "";

    /// <summary>Entry module (root-relative).</summary>
    public string Entry { get; set; } = "";

    /// <summary><c>iife</c> and/or <c>esm</c>.</summary>
    public List<string> Formats { get; set; } = ["iife", "esm"];
}
