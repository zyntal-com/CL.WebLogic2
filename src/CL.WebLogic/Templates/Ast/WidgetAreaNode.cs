namespace CL.WebLogic.Templates.Ast;

/// <summary><c>{widgetarea:name}</c> — every widget registered for an area.</summary>
public sealed class WidgetAreaNode : TemplateNode
{
    /// <summary>Creates the node.</summary>
    public WidgetAreaNode(string area) => Area = area;

    /// <summary>Area name.</summary>
    public string Area { get; }
}
