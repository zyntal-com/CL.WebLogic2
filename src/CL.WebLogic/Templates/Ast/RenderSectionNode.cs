namespace CL.WebLogic.Templates.Ast;

/// <summary><c>{rendersection:name}</c> — emits a section captured from the child template.</summary>
public sealed class RenderSectionNode : TemplateNode
{
    /// <summary>Creates the node.</summary>
    public RenderSectionNode(string name) => Name = name;

    /// <summary>Section name.</summary>
    public string Name { get; }
}
