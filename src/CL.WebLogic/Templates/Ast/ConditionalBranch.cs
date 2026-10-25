namespace CL.WebLogic.Templates.Ast;

/// <summary>One branch of an if/elseif chain.</summary>
public sealed class ConditionalBranch
{
    /// <summary>Creates the branch.</summary>
    public ConditionalBranch(TemplateCondition condition, IReadOnlyList<TemplateNode> body)
    {
        Condition = condition;
        Body = body;
    }

    /// <summary>The condition.</summary>
    public TemplateCondition Condition { get; }

    /// <summary>Rendered when the condition holds.</summary>
    public IReadOnlyList<TemplateNode> Body { get; }
}
