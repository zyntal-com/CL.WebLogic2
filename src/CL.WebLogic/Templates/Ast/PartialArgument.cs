namespace CL.WebLogic.Templates.Ast;

/// <summary>An argument of a parameterised partial: a quoted literal or a selector.</summary>
public sealed class PartialArgument
{
    private PartialArgument(string name, TemplateSelector? selector, string? literal)
    {
        Name = name;
        Selector = selector;
        Literal = literal;
    }

    /// <summary>An argument resolved from a selector at render time.</summary>
    public static PartialArgument FromSelector(string name, TemplateSelector selector) => new(name, selector, null);

    /// <summary>A literal argument.</summary>
    public static PartialArgument FromLiteral(string name, string literal) => new(name, null, literal);

    /// <summary>Argument name (read with <c>{param:name}</c>).</summary>
    public string Name { get; }

    /// <summary>Selector, when not literal.</summary>
    public TemplateSelector? Selector { get; }

    /// <summary>Literal value, when literal.</summary>
    public string? Literal { get; }

    /// <summary>True for a literal argument.</summary>
    public bool IsLiteral => Selector is null;
}
