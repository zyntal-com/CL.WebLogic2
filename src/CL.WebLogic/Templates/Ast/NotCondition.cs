namespace CL.WebLogic.Templates.Ast;

/// <summary>Negation (<c>{ifnot:…}</c>).</summary>
public sealed class NotCondition : TemplateCondition
{
    /// <summary>Creates the condition.</summary>
    public NotCondition(TemplateCondition inner) => Inner = inner;

    /// <summary>The negated condition.</summary>
    public TemplateCondition Inner { get; }
}
