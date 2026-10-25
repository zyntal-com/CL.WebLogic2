namespace CL.WebLogic.Templates.Ast;

/// <summary>A comparison operand: a selector or a literal.</summary>
public sealed class TemplateOperand
{
    private TemplateOperand(TemplateSelector? selector, string? literal)
    {
        Selector = selector;
        Literal = literal;
    }

    /// <summary>An operand resolved at render time.</summary>
    public static TemplateOperand FromSelector(TemplateSelector selector) => new(selector, null);

    /// <summary>A literal operand.</summary>
    public static TemplateOperand FromLiteral(string literal) => new(null, literal);

    /// <summary>The selector, when not literal.</summary>
    public TemplateSelector? Selector { get; }

    /// <summary>The literal, when literal.</summary>
    public string? Literal { get; }

    /// <summary>True for a literal operand.</summary>
    public bool IsLiteral => Selector is null;
}
