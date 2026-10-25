namespace CL.WebLogic.Templates.Ast;

/// <summary>An argument-less engine directive.</summary>
public sealed class KeywordNode : TemplateNode
{
    /// <summary>Creates the node.</summary>
    public KeywordNode(TemplateKeyword keyword) => Keyword = keyword;

    /// <summary>The directive.</summary>
    public TemplateKeyword Keyword { get; }
}
