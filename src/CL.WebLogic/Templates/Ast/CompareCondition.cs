namespace CL.WebLogic.Templates.Ast;

/// <summary><c>{if:model:count gt 10}</c> — numeric-aware comparison.</summary>
public sealed class CompareCondition : TemplateCondition
{
    /// <summary>Creates the condition.</summary>
    public CompareCondition(TemplateSelector left, CompareOp op, TemplateOperand right)
    {
        Left = left;
        Op = op;
        Right = right;
    }

    /// <summary>Left side.</summary>
    public TemplateSelector Left { get; }

    /// <summary>Operator.</summary>
    public CompareOp Op { get; }

    /// <summary>Right side.</summary>
    public TemplateOperand Right { get; }
}
