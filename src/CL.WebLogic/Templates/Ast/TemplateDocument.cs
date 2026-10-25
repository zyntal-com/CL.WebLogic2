namespace CL.WebLogic.Templates.Ast;

/// <summary>
/// A parsed template: an optional layout, the body nodes (with <c>{section}</c> blocks removed)
/// and the named sections the layout pulls in with <c>{rendersection}</c>.
/// </summary>
public sealed class TemplateDocument
{
    /// <summary>Creates the document.</summary>
    public TemplateDocument(string? layoutPath, IReadOnlyList<TemplateNode> nodes, IReadOnlyList<SectionDefinition> sections)
    {
        LayoutPath = layoutPath;
        Nodes = nodes;
        Sections = sections;
    }

    /// <summary>Layout path from a leading <c>{layout:…}</c>, or null.</summary>
    public string? LayoutPath { get; }

    /// <summary>Body nodes.</summary>
    public IReadOnlyList<TemplateNode> Nodes { get; }

    /// <summary>Named sections for the layout.</summary>
    public IReadOnlyList<SectionDefinition> Sections { get; }
}
