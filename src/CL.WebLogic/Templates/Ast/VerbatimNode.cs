namespace CL.WebLogic.Templates.Ast;

/// <summary><c>{verbatim}…{/verbatim}</c> — copied exactly, with no directive parsing.</summary>
public sealed class VerbatimNode : TemplateNode
{
    /// <summary>Creates the node.</summary>
    public VerbatimNode(string text) => Text = text;

    /// <summary>The text.</summary>
    public string Text { get; }
}
