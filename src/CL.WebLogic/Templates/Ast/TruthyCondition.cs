namespace CL.WebLogic.Templates.Ast;

/// <summary><c>{if:model:items}</c> — the selector resolves to a truthy value.</summary>
public sealed class TruthyCondition : TemplateCondition
{
    /// <summary>Creates the condition.</summary>
    public TruthyCondition(TemplateSelector selector) => Selector = selector;

    /// <summary>The selector.</summary>
    public TemplateSelector Selector { get; }
}
