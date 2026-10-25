namespace CL.WebLogic.Templates.Rendering;

/// <summary>Builds the (fingerprinted) URL for <c>{asset:path}</c>.</summary>
public interface ITemplateAssetUrlResolver
{
    /// <summary>URL of the theme asset at <paramref name="path"/>.</summary>
    string GetUrl(string path);
}
