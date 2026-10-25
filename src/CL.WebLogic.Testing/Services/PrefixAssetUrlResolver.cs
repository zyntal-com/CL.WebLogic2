using CL.WebLogic.Templates.Rendering;

namespace CL.WebLogic.Testing.Services;

/// <summary>Asset URLs as <c>{prefix}/{path}</c>, without fingerprints.</summary>
public sealed class PrefixAssetUrlResolver : ITemplateAssetUrlResolver
{
    private readonly string _prefix;

    /// <summary>Creates the resolver.</summary>
    public PrefixAssetUrlResolver(string prefix) => _prefix = (prefix ?? string.Empty).TrimEnd('/');

    /// <inheritdoc/>
    public string GetUrl(string path) => _prefix + "/" + (path ?? string.Empty).TrimStart('/');
}
