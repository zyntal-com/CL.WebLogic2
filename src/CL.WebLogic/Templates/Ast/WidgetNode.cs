namespace CL.WebLogic.Templates.Ast;

/// <summary><c>{widget:name attr="v"}</c> — a widget rendered at request time.</summary>
public sealed class WidgetNode : TemplateNode
{
    /// <summary>Creates the node.</summary>
    public WidgetNode(string name, IReadOnlyList<WidgetArgument> attributes)
    {
        Name = name;
        Attributes = attributes;
    }

    /// <summary>Widget name.</summary>
    public string Name { get; }

    /// <summary>Attributes passed to the widget.</summary>
    public IReadOnlyList<WidgetArgument> Attributes { get; }
}
