namespace CL.WebLogic.Templates.Ast;

/// <summary>
/// <c>{foreach:source as alias}…{empty}…{/foreach}</c>. The current item is available as
/// <c>{item:…}</c> and, with an alias, as <c>{alias:…}</c>; loop metadata as
/// <c>{loop:index|number|first|last|count|even|odd}</c>.
/// </summary>
public sealed class ForeachNode : TemplateNode
{
    /// <summary>Creates the node.</summary>
    public ForeachNode(TemplateSelector source, string? alias, IReadOnlyList<TemplateNode> body, IReadOnlyList<TemplateNode>? empty)
    {
        Source = source;
        Alias = alias;
        Body = body;
        Empty = empty;
    }

    /// <summary>The collection to iterate.</summary>
    public TemplateSelector Source { get; }

    /// <summary>Optional alias for the current item.</summary>
    public string? Alias { get; }

    /// <summary>Rendered once per item.</summary>
    public IReadOnlyList<TemplateNode> Body { get; }

    /// <summary>Rendered when the collection is empty (the <c>{empty}</c> branch), or null.</summary>
    public IReadOnlyList<TemplateNode>? Empty { get; }
}
