namespace CL.WebLogic.Templates.Ast;

/// <summary><c>{form:id attr="v"}</c> — a registered form rendered at request time.</summary>
public sealed class FormNode : TemplateNode
{
    /// <summary>Creates the node.</summary>
    public FormNode(string name, IReadOnlyList<WidgetArgument> attributes)
    {
        Name = name;
        Attributes = attributes;
    }

    /// <summary>Form id.</summary>
    public string Name { get; }

    /// <summary>Attributes passed to the form renderer (action, class, submit, renderer).</summary>
    public IReadOnlyList<WidgetArgument> Attributes { get; }
}
