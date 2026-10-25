namespace CL.WebLogic.Templates.Ast;

/// <summary>Template text emitted verbatim.</summary>
public sealed class LiteralNode : TemplateNode
{
    /// <summary>Creates the node.</summary>
    public LiteralNode(string text) => Text = text;

    /// <summary>The text.</summary>
    public string Text { get; }
}
