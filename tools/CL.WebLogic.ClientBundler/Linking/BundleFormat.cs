namespace CL.WebLogic.ClientBundler.Linking;

/// <summary>Output format of a bundle.</summary>
internal enum BundleFormat
{
    /// <summary>A classic script: an immediately invoked function.</summary>
    Iife,

    /// <summary>An ES module that re-exports the entry module's exports.</summary>
    Esm
}
