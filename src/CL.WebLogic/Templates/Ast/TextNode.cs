using CL.WebLogic.Templates.Escaping;

namespace CL.WebLogic.Templates.Ast;

/// <summary><c>{t:key}</c> — a localized text.</summary>
public sealed class TextNode : TemplateNode
{
    /// <summary>Creates the node.</summary>
    public TextNode(string key, TemplateEncodingContext context)
    {
        Key = key;
        Context = context;
    }

    /// <summary>Localization key.</summary>
    public string Key { get; }

    /// <summary>Output context.</summary>
    public TemplateEncodingContext Context { get; }
}
