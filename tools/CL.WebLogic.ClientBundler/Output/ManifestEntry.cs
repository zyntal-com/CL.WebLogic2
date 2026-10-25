namespace CL.WebLogic.ClientBundler.Output;

/// <summary>One file in <c>client-manifest.json</c>.</summary>
internal sealed record ManifestEntry(string Name, string Hash, string ContentType, string Integrity, int Length);
