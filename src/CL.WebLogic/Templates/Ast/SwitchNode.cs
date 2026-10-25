namespace CL.WebLogic.Templates.Ast;

/// <summary><c>{switch:sel}{case:v}…{/case}{default}…{/default}{/switch}</c>.</summary>
public sealed class SwitchNode : TemplateNode
{
    /// <summary>Creates the node.</summary>
    public SwitchNode(TemplateSelector subject, IReadOnlyList<SwitchCase> cases, IReadOnlyList<TemplateNode>? @default)
    {
        Subject = subject;
        Cases = cases;
        Default = @default;
    }

    /// <summary>The value switched on.</summary>
    public TemplateSelector Subject { get; }

    /// <summary>Cases in order.</summary>
    public IReadOnlyList<SwitchCase> Cases { get; }

    /// <summary>Rendered when no case matched, or null.</summary>
    public IReadOnlyList<TemplateNode>? Default { get; }
}
