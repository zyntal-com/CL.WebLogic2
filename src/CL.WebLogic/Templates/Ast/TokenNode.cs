using CL.WebLogic.Templates.Escaping;

namespace CL.WebLogic.Templates.Ast;

/// <summary>
/// A <c>{scope:selector|filter:arg}</c> value. <see cref="Context"/> was inferred at parse time
/// from the surrounding markup and decides how the value is encoded; <see cref="Raw"/>
/// (<c>{raw:…}</c>) disables encoding for trusted content.
/// </summary>
public sealed class TokenNode : TemplateNode
{
    /// <summary>Creates the node.</summary>
    public TokenNode(TemplateSelector selector, IReadOnlyList<TemplateFilter> filters, bool raw, TemplateEncodingContext context)
    {
        Selector = selector;
        Filters = filters;
        Raw = raw;
        Context = context;
    }

    /// <summary>What to resolve.</summary>
    public TemplateSelector Selector { get; }

    /// <summary>Filters applied in order.</summary>
    public IReadOnlyList<TemplateFilter> Filters { get; }

    /// <summary>True for <c>{raw:…}</c>: emitted without encoding.</summary>
    public bool Raw { get; }

    /// <summary>Output context the token appears in.</summary>
    public TemplateEncodingContext Context { get; }
}
