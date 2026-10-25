namespace CL.WebLogic.Templates.Ast;

/// <summary>One <c>key="value"</c> on a <c>{widget:…}</c>; the value may contain tokens.</summary>
public sealed class WidgetArgument
{
    /// <summary>Creates the attribute.</summary>
    public WidgetArgument(string name, IReadOnlyList<TemplateNode> value)
    {
        Name = name;
        Value = value;
    }

    /// <summary>Attribute name.</summary>
    public string Name { get; }

    /// <summary>Value parsed as an inline fragment (literals and tokens).</summary>
    public IReadOnlyList<TemplateNode> Value { get; }
}
