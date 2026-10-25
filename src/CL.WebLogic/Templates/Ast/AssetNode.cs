using CL.WebLogic.Templates.Escaping;

namespace CL.WebLogic.Templates.Ast;

/// <summary><c>{asset:css/site.css}</c> — the fingerprinted URL of a theme asset.</summary>
public sealed class AssetNode : TemplateNode
{
    /// <summary>Creates the node.</summary>
    public AssetNode(string path, TemplateEncodingContext context)
    {
        Path = path;
        Context = context;
    }

    /// <summary>Path inside the assets folder.</summary>
    public string Path { get; }

    /// <summary>Output context.</summary>
    public TemplateEncodingContext Context { get; }
}
